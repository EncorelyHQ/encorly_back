namespace EncorelyApplication.Interfaces;

public interface IEventProducer<T>
{
    Task ProduceAsync(string topic, T message, CancellationToken ct = default);
}
