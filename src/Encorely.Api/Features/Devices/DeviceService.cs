using Encorely.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Encorely.Api.Features.Devices;

public sealed class DeviceService(AppDbContext db, TimeProvider clock)
{
    public async Task RegisterAsync(Guid userId, PushTokenRequest request, CancellationToken ct)
    {
        try
        {
            await UpsertAsync(userId, request, ct);
        }
        // Dos registros simultáneos del mismo token: el segundo ya ve la fila y solo la actualiza.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            await UpsertAsync(userId, request, ct);
        }
    }

    // Borra solo si el token es mío; el 204 idempotente no revela si el token existía a nombre de otro.
    public async Task UnregisterAsync(Guid userId, DeletePushTokenRequest request, CancellationToken ct)
        => await db.DevicePushTokens.Where(t => t.Token == request.Token && t.UserId == userId).ExecuteDeleteAsync(ct);

    private async Task UpsertAsync(Guid userId, PushTokenRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var existing = await db.DevicePushTokens.FirstOrDefaultAsync(t => t.Token == request.Token, ct);
        if (existing is null)
        {
            db.DevicePushTokens.Add(new DevicePushToken { UserId = userId, Token = request.Token, Platform = request.Platform, CreatedAt = now, UpdatedAt = now });
        }
        else
        {
            existing.UserId = userId;
            existing.Platform = request.Platform;
            existing.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }
}
