using Encorely.Api.Features.Chat;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Features.Sync;
using Encorely.Api.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Infrastructure.Database;

/// <summary>
/// Limpieza periódica: buzones de vinculación vencidos, refresh tokens caducados hace más de 30 días, la retención de
/// mensajes cifrados (si está configurada), el franking vencido y el texto de la evidencia de reportes viejos.
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
        var franking = await db.MessageFrankingRecords.Where(f => f.CreatedAt <= now.AddDays(-MessageFrankingRecord.RetentionDays)).ExecuteDeleteAsync(ct)
            + await db.DeliveredMessageIds.Where(d => d.DeliveredAt <= now.AddDays(-MessageFrankingRecord.RetentionDays)).ExecuteDeleteAsync(ct);
        var reports = await PurgeReportEvidenceAsync(db, now, ct);
        if (links + tokens + messages + franking + reports > 0)
            logger.LogInformation(
                "Limpieza: {Links} vinculaciones, {Tokens} refresh tokens, {Messages} mensajes, {Franking} compromisos de franking o ids entregados y {Reports} evidencias de reportes vencidos",
                links, tokens, messages, franking, reports);
    }

    /// <summary>
    /// El texto revelado en un reporte es dato personal de ambas partes: se borra a los <see cref="Report.EvidenceRetentionDays"/>
    /// días de resolverse, o en cuanto la cuenta reportada ya no existe (no queda a quién moderar).
    /// </summary>
    private static async Task<int> PurgeReportEvidenceAsync(AppDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-Report.EvidenceRetentionDays);
        var total = 0;
        while (true)
        {
            var batch = await db.Reports
                .Where(r => r.EvidencePurgedAt == null
                            && (r.ReportedUserId == null || (r.Status != ReportStatus.Open && r.ResolvedAt <= cutoff)))
                .OrderBy(r => r.Id).Take(200).ToListAsync(ct);
            if (batch.Count == 0) return total;
            foreach (var r in batch)
            {
                r.Detail = null;
                foreach (var e in r.Evidence) e.Plaintext = "";
                r.EvidencePurgedAt = now;
                if (r.Status == ReportStatus.Open)
                {
                    r.Status = ReportStatus.Dismissed;
                    r.ResolutionNote = "Cerrado automáticamente: la cuenta reportada se eliminó.";
                    r.ResolvedAt = now;
                }
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            total += batch.Count;
        }
    }
}
