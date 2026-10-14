using Encorely.Api.Features.Chat;
using Encorely.Api.Features.Sync;
using Encorely.Api.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Infrastructure.Database;

/// <summary>
/// Limpieza periódica: buzones de vinculación vencidos, refresh tokens caducados hace más de 30 días y, si está
/// configurada, la retención de mensajes cifrados.
/// </summary>
public sealed class MaintenanceService(IServiceScopeFactory scopes, IOptions<ChatOptions> chat, TimeProvider clock, ILogger<MaintenanceService> logger)
    : BackgroundService
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
        var messages = chat.Value.MessageRetentionDays > 0
            ? await db.ChatMessages.Where(m => m.CreatedAt <= now.AddDays(-chat.Value.MessageRetentionDays)).ExecuteDeleteAsync(ct)
            : 0;
        var franking = await db.MessageFrankingRecords.Where(f => f.CreatedAt <= now.AddDays(-MessageFrankingRecord.RetentionDays)).ExecuteDeleteAsync(ct);
        if (links + tokens + messages + franking > 0)
            logger.LogInformation("Limpieza: {Links} vinculaciones, {Tokens} refresh tokens, {Messages} mensajes y {Franking} compromisos de franking vencidos",
                links, tokens, messages, franking);
    }
}
