using Encorely.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Infrastructure.Push;

/// <summary>Envía push sin contenido vía Expo Push API (que entrega por FCM/APNs).</summary>
public sealed class ExpoPushSender(IServiceScopeFactory scopes, IHttpClientFactory http, IOptions<PushOptions> options, ILogger<ExpoPushSender> logger) : IPushSender
{
    public Task SendAsync(Guid userId, PushNotification notification, CancellationToken ct = default) => throw new NotImplementedException();
}
