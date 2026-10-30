using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Api.Infrastructure.Auth;

/// <summary>
/// Único camino para cerrar sesiones: revoca la familia de refresh, borra los push tokens ligados a ella y corta el hub.
/// El access token de la sesión deja de servir en el acto porque <see cref="AuthSetup.UseActiveUserCheck"/> exige que su
/// <c>sid</c> siga abierto.
/// </summary>
public sealed class SessionRevoker(AppDbContext db, HubConnections connections, TimeProvider clock)
{
    /// <returns>Refresh tokens revocados: 0 si la sesión no existe, es de otro usuario o ya estaba cerrada.</returns>
    public async Task<int> RevokeAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var revoked = await RevokeWhere(db.RefreshTokens.Where(t => t.UserId == userId && t.FamilyId == sessionId), ct);
        await db.DevicePushTokens.Where(t => t.UserId == userId && t.SessionId == sessionId).ExecuteDeleteAsync(ct);
        connections.Abort(userId, sessionId);
        return revoked;
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken ct)
    {
        await RevokeWhere(db.RefreshTokens.Where(t => t.UserId == userId), ct);
        await db.DevicePushTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        connections.Abort(userId);
    }

    private Task<int> RevokeWhere(IQueryable<Features.Auth.RefreshToken> tokens, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return tokens.Where(t => t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }
}
