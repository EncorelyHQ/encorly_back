namespace Encorely.Api.Infrastructure.Push;

public enum PushKind { NewMessage, NewMatch }

/// <summary>Push SIN contenido: nunca lleva texto de mensajes ni nombres; solo el tipo y el id del match.</summary>
public sealed record PushNotification(PushKind Kind, Guid MatchId);

public interface IPushSender
{
    Task SendAsync(Guid userId, PushNotification notification, CancellationToken ct = default);
}

/// <summary>Push desactivado (local/tests).</summary>
public sealed class NoopPushSender : IPushSender
{
    public Task SendAsync(Guid userId, PushNotification notification, CancellationToken ct = default) => Task.CompletedTask;
}
