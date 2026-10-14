using Encorely.Api.Features.Music;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Encorely.Api.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Api.Features.Matching;

public sealed class MatchingService(AppDbContext db, Notifier notifier, TimeProvider clock)
{
    public async Task<LikeResponse> LikeAsync(Guid userId, LikeRequest request, CancellationToken ct)
    {
        var targetId = request.TargetUserId;
        if (targetId == userId)
            throw ApiException.BadRequest(ErrorCodes.CannotLikeSelf, "No puedes dar like a tu propio perfil.");

        var targetActive = await db.Users.AnyAsync(u => u.Id == targetId && u.Status == UserStatus.Active, ct);
        // El bloqueo se responde igual que "no existe" para no revelarlo.
        if (!targetActive || await IsBlockedAsync(userId, targetId, ct))
            throw UserNotFound();

        var now = clock.GetUtcNow();
        var cutoff = now.AddDays(-1);
        var sharedEvents = await (
            from mine in db.EventAttendances
            join theirs in db.EventAttendances on mine.EventId equals theirs.EventId
            join e in db.Events on mine.EventId equals e.Id
            where mine.UserId == userId && theirs.UserId == targetId && e.IsPublished && e.StartsAt > cutoff
            select e.Id).ToListAsync(ct);
        if (sharedEvents.Count == 0 || (request.EventId is { } eventId && !sharedEvents.Contains(eventId)))
            throw ApiException.Forbidden(ErrorCodes.NoSharedEvent, "Solo puedes dar like a alguien que va a un evento contigo.");

        var (a, b) = Match.OrderPair(userId, targetId);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await PairLock.AcquireAsync(db, userId, targetId, ct);

        // Bajo el lock el bloqueo pudo aparecer entre la validación y aquí.
        if (await IsBlockedAsync(userId, targetId, ct)) throw UserNotFound();

        var like = await db.UserLikes.FirstOrDefaultAsync(l => l.FromUserId == userId && l.ToUserId == targetId, ct);
        if (like is null)
        {
            like = new UserLike { FromUserId = userId, ToUserId = targetId, EventId = request.EventId, CreatedAt = now };
            db.UserLikes.Add(like);
            await db.SaveChangesAsync(ct);
        }

        var existing = await db.Matches.FirstOrDefaultAsync(m => m.UserAId == a && m.UserBId == b && m.Status == MatchStatus.Active, ct);
        if (existing is not null)
        {
            await tx.CommitAsync(ct);
            return new LikeResponse(true, existing.Id);
        }

        var reciprocal = await db.UserLikes.FirstOrDefaultAsync(l => l.FromUserId == targetId && l.ToUserId == userId, ct);
        if (reciprocal is null)
        {
            await tx.CommitAsync(ct);
            return new LikeResponse(false, null);
        }

        var match = new Match { UserAId = a, UserBId = b, EventId = like.EventId ?? reciprocal.EventId, CreatedAt = now };
        db.Matches.Add(match);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await notifier.MatchCreatedAsync(match.Id, a, b);
        return new LikeResponse(true, match.Id);
    }

    public async Task<IReadOnlyList<MatchDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        var matches = await db.Matches.AsNoTracking()
            .Where(m => m.Status == MatchStatus.Active && (m.UserAId == userId || m.UserBId == userId))
            .OrderByDescending(m => m.LastMessageAt ?? m.CreatedAt)
            .ToListAsync(ct);
        return await ToDtosAsync(userId, matches, ct);
    }

    public async Task<MatchDto> GetAsync(Guid userId, Guid matchId, CancellationToken ct)
    {
        var match = await db.Matches.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == matchId && (m.UserAId == userId || m.UserBId == userId), ct)
            ?? throw MatchNotFound();
        return (await ToDtosAsync(userId, [match], ct))[0];
    }

    public async Task UnmatchAsync(Guid userId, Guid matchId, CancellationToken ct)
    {
        var probe = await db.Matches.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == matchId && (m.UserAId == userId || m.UserBId == userId) && m.Status == MatchStatus.Active, ct)
            ?? throw MatchNotFound();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await PairLock.AcquireAsync(db, probe.UserAId, probe.UserBId, ct);

        var match = await db.Matches.FirstOrDefaultAsync(m => m.Id == matchId && m.Status == MatchStatus.Active, ct)
            ?? throw MatchNotFound();
        match.Status = MatchStatus.Closed;
        match.CloseReason = MatchCloseReason.Unmatched;
        match.ClosedAt = clock.GetUtcNow();
        match.ClosedByUserId = userId;
        await db.SaveChangesAsync(ct);

        var (a, b) = (match.UserAId, match.UserBId);
        // Sin likes previos, volver a hacer match exige que ambos den like otra vez.
        await db.UserLikes.Where(l => (l.FromUserId == a && l.ToUserId == b) || (l.FromUserId == b && l.ToUserId == a)).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        await notifier.MatchClosedAsync(match.Id, a, b);
    }

    private Task<bool> IsBlockedAsync(Guid x, Guid y, CancellationToken ct)
        => db.Blocks.AnyAsync(b => (b.BlockerId == x && b.BlockedId == y) || (b.BlockerId == y && b.BlockedId == x), ct);

    private static ApiException UserNotFound() => ApiException.NotFound(ErrorCodes.UserNotFound, "No encontramos a esa persona.");

    private static ApiException MatchNotFound() => ApiException.NotFound(ErrorCodes.MatchNotFound, "No encontramos ese match.");

    private async Task<IReadOnlyList<MatchDto>> ToDtosAsync(Guid userId, List<Match> matches, CancellationToken ct)
    {
        if (matches.Count == 0) return [];

        var peerIds = matches.Select(m => m.PeerOf(userId)).Distinct().ToList();
        var allIds = peerIds.Append(userId).ToList();
        var eventIds = matches.Where(m => m.EventId != null).Select(m => m.EventId!.Value).Distinct().ToList();

        var peers = await db.Users.AsNoTracking().Where(u => peerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.BirthDate, u.City }).ToDictionaryAsync(u => u.Id, ct);
        var scores = (await db.UserGenreScores.AsNoTracking().Where(s => allIds.Contains(s.UserId)).ToListAsync(ct))
            .ToLookup(s => s.UserId);
        var declared = (await db.UserGenres.AsNoTracking().Where(g => allIds.Contains(g.UserId))
                .Select(g => new { g.UserId, g.GenreId }).ToListAsync(ct))
            .ToLookup(g => g.UserId, g => g.GenreId);
        var slugs = await db.Genres.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Slug, ct);
        var eventNames = await db.Events.AsNoTracking().Where(e => eventIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        // "Hoy" en Colombia (UTC-5, sin horario de verano).
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(-5));
        var mine = Dna.Vector(scores[userId], declared[userId]);

        return matches.Select(m =>
        {
            var peerId = m.PeerOf(userId);
            peers.TryGetValue(peerId, out var peer);
            var theirs = Dna.Vector(scores[peerId], declared[peerId]);
            var shared = Dna.Shared(mine, theirs).Where(slugs.ContainsKey).Select(id => slugs[id]).ToList();
            return new MatchDto(
                m.Id,
                new PeerDto(peerId, peer?.DisplayName ?? "", peer?.BirthDate is { } bd ? User.AgeOn(bd, today) : null, peer?.City),
                m.EventId is { } e && eventNames.TryGetValue(e, out var name) ? name : null,
                shared, m.Status, m.CreatedAt, m.LastMessageAt, m.LastSeq);
        }).ToList();
    }
}
