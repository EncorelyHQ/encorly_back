using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Infrastructure.Push;

/// <summary>
/// Consume la cola y llama a la Expo Push API. Nunca registra tokens ni contenido; solo ids y códigos de estado.
/// </summary>
public sealed class PushDispatcher(PushQueue queue, IServiceScopeFactory scopes, IHttpClientFactory http, IOptions<PushOptions> options, ILogger<PushDispatcher> logger) : BackgroundService
{
    private const int ExpoBatchLimit = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.ReadAllAsync(stoppingToken))
        {
            try { await ProcessAsync(item, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // Solo el tipo: el mensaje de una excepción HTTP podría arrastrar datos del request.
                logger.LogWarning("Falló el envío push al usuario {UserId} ({Error})", item.UserId, ex.GetType().Name);
            }
        }
    }

    public async Task ProcessAsync(QueuedPush item, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokens = await db.DevicePushTokens.AsNoTracking().Where(t => t.UserId == item.UserId).Select(t => t.Token).ToListAsync(ct);
        if (tokens.Count == 0) return;

        var opts = options.Value;
        var client = http.CreateClient(PushServiceCollectionExtensions.HttpClientName);

        foreach (var batch in tokens.Chunk(ExpoBatchLimit))
        {
            var payload = batch.Select(to => new
            {
                to,
                title = "Encorely",
                body = item.Notification.Kind == PushKind.NewMatch ? "Tienes un match nuevo" : "Tienes mensajes nuevos",
                data = new
                {
                    type = item.Notification.Kind == PushKind.NewMatch ? "newMatch" : "newMessage",
                    matchId = item.Notification.MatchId,
                },
                sound = "default",
                priority = "high",
            }).ToArray();

            using var request = new HttpRequestMessage(HttpMethod.Post, opts.ExpoEndpoint) { Content = JsonContent.Create(payload) };
            if (!string.IsNullOrWhiteSpace(opts.ExpoAccessToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.ExpoAccessToken);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Expo Push respondió {Status} para el usuario {UserId}", (int)response.StatusCode, item.UserId);
                continue;
            }

            await PruneDeadTokensAsync(db, batch, response, ct);
        }
    }

    private async Task PruneDeadTokensAsync(AppDbContext db, string[] batch, HttpResponseMessage response, CancellationToken ct)
    {
        JsonElement tickets;
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (!body.TryGetProperty("data", out tickets) || tickets.ValueKind != JsonValueKind.Array) return;
        }
        catch (JsonException)
        {
            logger.LogWarning("Expo Push devolvió una respuesta ilegible");
            return;
        }

        // Los tickets llegan en el mismo orden que los mensajes enviados.
        var i = 0;
        foreach (var ticket in tickets.EnumerateArray())
        {
            if (i >= batch.Length) break;
            var token = batch[i++];
            if (ticket.TryGetProperty("status", out var status) && status.GetString() == "error"
                && ticket.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object
                && details.TryGetProperty("error", out var error) && error.GetString() == "DeviceNotRegistered")
                await db.DevicePushTokens.Where(t => t.Token == token).ExecuteDeleteAsync(ct);
        }
    }
}
