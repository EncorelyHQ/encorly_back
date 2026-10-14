using System.Security.Claims;
using Encorely.Api.Features.Auth;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Api.Features.Sessions;

/// <summary>Una sesión = una familia de refresh tokens (un dispositivo con sesión iniciada).</summary>
public sealed record SessionDto(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset LastUsedAt, DateTimeOffset ExpiresAt);

public static class SessionEndpoints
{
    public static void MapSessions(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/me/sessions").WithTags("Sesiones");
        g.MapGet("", List).WithName("ListSessions").WithSummary("Mis sesiones abiertas (dispositivos con sesión)").Problems(401);
        g.MapDelete("/{id:guid}", Revoke).WithName("RevokeSession").WithSummary("Cierra una sesión (p. ej. un teléfono perdido)").Problems(401, 404);
        g.MapDelete("", RevokeAll).WithName("RevokeAllSessions").WithSummary("Cierra todas mis sesiones, incluida esta").Problems(401);
    }

    private static async Task<Ok<IReadOnlyList<SessionDto>>> List(ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId();
        var now = clock.GetUtcNow();
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new { t.FamilyId, t.CreatedAt, t.ExpiresAt })
            .ToListAsync(ct);
        var familyIds = active.Select(t => t.FamilyId).ToList();
        var startedAt = await db.RefreshTokens.Where(t => familyIds.Contains(t.FamilyId))
            .GroupBy(t => t.FamilyId).Select(g => new { g.Key, Started = g.Min(t => t.CreatedAt) })
            .ToDictionaryAsync(x => x.Key, x => x.Started, ct);
        var sessions = active.Select(t => new SessionDto(t.FamilyId, startedAt[t.FamilyId], t.CreatedAt, t.ExpiresAt)).ToList();
        return TypedResults.Ok<IReadOnlyList<SessionDto>>(sessions);
    }

    private static async Task<NoContent> Revoke(Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId();
        var revoked = await RevokeWhere(db.RefreshTokens.Where(t => t.UserId == userId && t.FamilyId == id), clock, ct);
        if (revoked == 0) throw ApiException.NotFound("session_not_found", "Esa sesión no existe o ya está cerrada.");
        // Sin esto el teléfono "cerrado" seguiría recibiendo avisos de mensajes y matches.
        await db.DevicePushTokens.Where(t => t.UserId == userId && t.SessionId == id).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RevokeAll(ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId();
        await RevokeWhere(db.RefreshTokens.Where(t => t.UserId == userId), clock, ct);
        await db.DevicePushTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    private static Task<int> RevokeWhere(IQueryable<RefreshToken> tokens, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return tokens.Where(t => t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }
}
