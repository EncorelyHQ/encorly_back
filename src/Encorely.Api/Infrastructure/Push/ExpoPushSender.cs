namespace Encorely.Api.Infrastructure.Push;

/// <summary>Encola push sin contenido; el envío real (Expo → FCM/APNs) lo hace <see cref="PushDispatcher"/> fuera de la request.</summary>
public sealed class ExpoPushSender(PushQueue queue, ILogger<ExpoPushSender> logger) : IPushSender
{
    public Task SendAsync(Guid userId, PushNotification notification, CancellationToken ct = default)
    {
        if (!queue.TryEnqueue(new QueuedPush(userId, notification)))
            logger.LogWarning("Cola de push no disponible; se omite un aviso para el usuario {UserId}", userId);
        return Task.CompletedTask;
    }
}
