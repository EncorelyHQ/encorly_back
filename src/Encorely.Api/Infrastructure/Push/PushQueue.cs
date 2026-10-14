using System.Threading.Channels;

namespace Encorely.Api.Infrastructure.Push;

public sealed record QueuedPush(Guid UserId, PushNotification Notification);

/// <summary>Cola en memoria entre las requests (productor) y el <see cref="PushDispatcher"/> (consumidor).</summary>
public sealed class PushQueue
{
    // Acotada: si Expo se cae, se descartan los más viejos en vez de crecer sin límite. El REST sigue siendo la fuente de verdad.
    private readonly Channel<QueuedPush> _channel = Channel.CreateBounded<QueuedPush>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public bool TryEnqueue(QueuedPush item) => _channel.Writer.TryWrite(item);
    public IAsyncEnumerable<QueuedPush> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public static class PushServiceCollectionExtensions
{
    public const string HttpClientName = "expo-push";

    /// <summary>Registra la cola, el dispatcher y el HttpClient con nombre "expo-push".</summary>
    public static IServiceCollection AddExpoPush(this IServiceCollection services)
    {
        services.AddSingleton<PushQueue>();
        services.AddHttpClient(HttpClientName);
        services.AddHostedService<PushDispatcher>();
        return services;
    }
}
