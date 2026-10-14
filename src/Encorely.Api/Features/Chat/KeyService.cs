using Encorely.Api.Features.Matching;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Encorely.Api.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Encorely.Api.Features.Chat;

public sealed class KeyService(AppDbContext db, Notifier notifier, TimeProvider clock)
{
    public async Task<KeyBundleDto> PutAsync(Guid userId, UploadKeysRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serializa las rotaciones del mismo usuario: en paralelo todas veían el mismo máximo y la última en escribir
        // (no la de id mayor) dejaba CurrentBoxKeyId retrocedido.
        await AdvisoryLock.AcquireAsync(db, "keys", userId, ct);
        var current = await db.UserKeys.AsNoTracking().FirstOrDefaultAsync(k => k.UserId == userId, ct);

        if (current is not null)
        {
            var sameId = await db.UserBoxKeys.AsNoTracking()
                .FirstOrDefaultAsync(k => k.UserId == userId && k.BoxKeyId == request.BoxKeyId, ct);
            // Re-PUT idéntico (reintento tras timeout): éxito sin tocar nada ni avisar a nadie.
            if (sameId is not null && current.CurrentBoxKeyId == sameId.BoxKeyId
                && current.IdentityKey.AsSpan().SequenceEqual(request.IdentityKey)
                && sameId.BoxKey.AsSpan().SequenceEqual(request.BoxKey)
                && sameId.Signature.AsSpan().SequenceEqual(request.BoxKeySig))
                return ToBundle(current, sameId);

            var max = await db.UserBoxKeys.Where(k => k.UserId == userId).MaxAsync(k => (int?)k.BoxKeyId, ct) ?? 0;
            if (request.BoxKeyId <= max) throw Stale(max);

            // Condicionado a que el id solo suba: defensa por si algún camino llegara aquí sin el lock.
            var updated = await db.UserKeys.Where(k => k.UserId == userId && k.CurrentBoxKeyId < request.BoxKeyId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(k => k.IdentityKey, request.IdentityKey)
                    .SetProperty(k => k.CurrentBoxKeyId, request.BoxKeyId)
                    .SetProperty(k => k.UpdatedAt, now), ct);
            if (updated == 0) throw Stale(max);
            current = new UserKey { UserId = userId, IdentityKey = request.IdentityKey, CurrentBoxKeyId = request.BoxKeyId, UpdatedAt = now };
        }
        else
        {
            current = new UserKey { UserId = userId, IdentityKey = request.IdentityKey, CurrentBoxKeyId = request.BoxKeyId, UpdatedAt = now };
            db.UserKeys.Add(current);
        }

        var box = new UserBoxKey
        {
            UserId = userId,
            BoxKeyId = request.BoxKeyId,
            IdentityKey = request.IdentityKey,
            BoxKey = request.BoxKey,
            Signature = request.BoxKeySig,
            CreatedAt = now,
        };
        db.UserBoxKeys.Add(box);

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        // Defensa: con el lock no debería ocurrir, pero un id repetido sigue siendo 409 y no un 500.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw Stale(request.BoxKeyId);
        }

        var peers = await db.Matches.AsNoTracking()
            .Where(m => m.Status == MatchStatus.Active && (m.UserAId == userId || m.UserBId == userId))
            .Select(m => new { m.Id, Peer = m.UserAId == userId ? m.UserBId : m.UserAId })
            .ToListAsync(ct);
        foreach (var m in peers) await notifier.PeerKeyChangedAsync(m.Peer, m.Id, userId);

        return ToBundle(current, box);
    }

    public async Task<KeyBundleDto> GetMineAsync(Guid userId, CancellationToken ct)
        => await GetBundleAsync(userId, ct) ?? throw ApiException.NotFound(ErrorCodes.KeysMissing, "Aún no has publicado tus llaves.");

    public async Task<KeyBundleDto> GetPeerAsync(Guid userId, Guid matchId, CancellationToken ct)
    {
        var peerId = await PeerOfAsync(userId, matchId, ct);
        return await GetBundleAsync(peerId, ct) ?? throw ApiException.NotFound(ErrorCodes.PeerKeysMissing, "La otra persona aún no publica sus llaves.");
    }

    public async Task<BoxKeyDto> GetPeerBoxKeyAsync(Guid userId, Guid matchId, int boxKeyId, CancellationToken ct)
    {
        var peerId = await PeerOfAsync(userId, matchId, ct);
        var key = await db.UserBoxKeys.AsNoTracking().FirstOrDefaultAsync(k => k.UserId == peerId && k.BoxKeyId == boxKeyId, ct)
            ?? throw ApiException.NotFound(ErrorCodes.NotFound, "Llave no encontrada.");
        return new BoxKeyDto(key.UserId, key.BoxKeyId, key.IdentityKey, key.BoxKey, key.Signature, key.CreatedAt);
    }

    private async Task<Guid> PeerOfAsync(Guid userId, Guid matchId, CancellationToken ct)
    {
        var match = await db.Matches.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == matchId && (m.UserAId == userId || m.UserBId == userId), ct)
            ?? throw ApiException.NotFound(ErrorCodes.MatchNotFound, "Match no encontrado.");
        return match.PeerOf(userId);
    }

    private async Task<KeyBundleDto?> GetBundleAsync(Guid userId, CancellationToken ct)
    {
        var current = await db.UserKeys.AsNoTracking().FirstOrDefaultAsync(k => k.UserId == userId, ct);
        if (current is null) return null;
        var box = await db.UserBoxKeys.AsNoTracking().FirstOrDefaultAsync(k => k.UserId == userId && k.BoxKeyId == current.CurrentBoxKeyId, ct);
        return box is null ? null : ToBundle(current, box);
    }

    private static KeyBundleDto ToBundle(UserKey key, UserBoxKey box)
        => new(key.UserId, key.IdentityKey, box.BoxKey, box.BoxKeyId, box.Signature, key.UpdatedAt);

    private static ApiException Stale(int max)
        => ApiException.Conflict(ErrorCodes.StaleBoxKeyId, $"Usa un boxKeyId mayor que {max}.", $"Vigente: {max}.");
}
