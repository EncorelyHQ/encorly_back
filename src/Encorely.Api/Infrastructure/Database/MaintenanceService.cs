using Encorely.Api.Features.Sync;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Api.Infrastructure.Database;

/// <summary>Limpieza periódica: buzones de vinculación vencidos y refresh tokens caducados hace más de 30 días.</summary>
public sealed class MaintenanceService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<MaintenanceService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Falló la limpieza periódica");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow();
        var links = await db.Set<DeviceLinkRequest>().Where(l => l.ExpiresAt <= now).ExecuteDeleteAsync(ct);
        var tokens = await db.RefreshTokens.Where(t => t.ExpiresAt <= now.AddDays(-30)).ExecuteDeleteAsync(ct);
        if (links + tokens > 0) logger.LogInformation("Limpieza: {Links} vinculaciones y {Tokens} refresh tokens", links, tokens);
    }
}
