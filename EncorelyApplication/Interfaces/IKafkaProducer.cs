namespace EncorelyApplication.Interfaces;

public interface IKafkaProducer<T> where T : class
{
    ValueTask ProduceAsync(string topic, T message, CancellationToken cancellationToken = default);
}
