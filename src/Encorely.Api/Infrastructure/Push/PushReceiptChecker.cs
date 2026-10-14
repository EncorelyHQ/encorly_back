using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Infrastructure.Push;

/// <summary>
/// Consulta periódicamente los receipts de Expo y borra los tokens que FCM/APNs reportan como DeviceNotRegistered
/// (el ticket de envío solo dice que Expo aceptó el mensaje). Nunca registra tokens.
/// </summary>
public sealed class PushReceiptChecker(
    PushReceiptTracker tracker, IServiceScopeFactory scopes, IHttpClientFactory http,
    IOptions<PushOptions> options, TimeProvider clock, ILogger<PushReceiptChecker> logger) : BackgroundService
{
    private const int ReceiptBatchLimit = 1000;
    private const string SendPath = "/push/send";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.ReceiptCheckInterval;
        if (interval <= TimeSpan.Zero) return;
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { await CheckAsync(stoppingToken); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning("Falló la consulta de receipts push ({Error})", ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public async Task CheckAsync(CancellationToken ct)
    {
        var pending = tracker.Snapshot(clock.GetUtcNow());
        if (pending.Count == 0) return;

        var opts = options.Value;
        if (!opts.ExpoEndpoint.Contains(SendPath, StringComparison.Ordinal))
        {
            logger.LogWarning("Push:ExpoEndpoint no contiene {Path}; no se pueden derivar los receipts", SendPath);
            return;
        }
        var receiptsUrl = opts.ExpoEndpoint.Replace(SendPath, "/push/getReceipts", StringComparison.Ordinal);
        var client = http.CreateClient(PushServiceCollectionExtensions.HttpClientName);

        foreach (var batch in pending.Chunk(ReceiptBatchLimit))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, receiptsUrl) { Content = JsonContent.Create(new { ids = batch.Select(p => p.TicketId) }) };
            if (!string.IsNullOrWhiteSpace(opts.ExpoAccessToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.ExpoAccessToken);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Expo getReceipts respondió {Status}", (int)response.StatusCode);
                continue;
            }

            JsonElement data;
            try
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (!body.TryGetProperty("data", out data) || data.ValueKind != JsonValueKind.Object) continue;
            }
            catch (JsonException)
            {
                logger.LogWarning("Expo getReceipts devolvió una respuesta ilegible");
                continue;
            }

            // Los ids que Expo aún no resolvió no vienen en data: se quedan para el siguiente ciclo (hasta 24 h).
            var resolved = new HashSet<string>();
            var dead = new List<string>();
            foreach (var p in batch)
            {
                if (!data.TryGetProperty(p.TicketId, out var receipt) || receipt.ValueKind != JsonValueKind.Object) continue;
                resolved.Add(p.TicketId);
                if (receipt.TryGetProperty("status", out var status) && status.GetString() == "error"
                    && receipt.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object
                    && details.TryGetProperty("error", out var error) && error.GetString() == "DeviceNotRegistered")
                    dead.Add(p.Token);
            }

            if (dead.Count > 0)
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.DevicePushTokens.Where(t => dead.Contains(t.Token)).ExecuteDeleteAsync(ct);
            }
            tracker.Remove(resolved);
        }
    }
}
