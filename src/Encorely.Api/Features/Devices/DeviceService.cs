using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Encorely.Api.Features.Devices;

public sealed class DeviceService(AppDbContext db, SessionRevoker sessions, TimeProvider clock)
{
    /// <param name="sessionId">Claim <c>sid</c> del access token: el token push se borra cuando esa sesión se cierra.</param>
    public async Task RegisterAsync(Guid userId, Guid? sessionId, PushTokenRequest request, CancellationToken ct)
    {
        // Un access token vivo de una sesión ya cerrada no puede volver a registrar push tokens (quedarían huérfanos).
        if (sessionId is { } sid && !await db.RefreshTokens.AnyAsync(t => t.FamilyId == sid && t.UserId == userId && t.RevokedAt == null, ct))
            throw ApiException.Unauthorized(ErrorCodes.Unauthorized, "Tu sesión ya no es válida.");

        try
        {
            await UpsertAsync(userId, sessionId, request, ct);
        }
        // Dos registros simultáneos del mismo token: el segundo ya ve la fila y solo la actualiza.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            await UpsertAsync(userId, sessionId, request, ct);
        }
    }

    // Borra solo si el token es mío; el 204 idempotente no revela si el token existía a nombre de otro.
    public async Task UnregisterAsync(Guid userId, DeletePushTokenRequest request, CancellationToken ct)
        => await db.DevicePushTokens.Where(t => t.Token == request.Token && t.UserId == userId).ExecuteDeleteAsync(ct);

    private async Task UpsertAsync(Guid userId, Guid? sessionId, PushTokenRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var existing = await db.DevicePushTokens.FirstOrDefaultAsync(t => t.Token == request.Token, ct);
        if (existing is not null && existing.UserId != userId)
        {
            // El teléfono cambió de cuenta. La app guarda una sola sesión, así que la del dueño anterior en ese dispositivo
            // ya no se usa: se cierra en vez de dejarla abierta y sin avisos. RevokeAsync también borra la fila del token.
            if (existing.SessionId is { } previous) await sessions.RevokeAsync(existing.UserId, previous, ct);
            await db.DevicePushTokens.Where(t => t.Token == request.Token).ExecuteDeleteAsync(ct);
            db.Entry(existing).State = EntityState.Detached;
            existing = null;
        }
        if (existing is null)
        {
            db.DevicePushTokens.Add(new DevicePushToken
            {
                UserId = userId, SessionId = sessionId, Token = request.Token, Platform = request.Platform, CreatedAt = now, UpdatedAt = now,
            });
        }
        else
        {
            existing.SessionId = sessionId;
            existing.Platform = request.Platform;
            existing.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }
}
