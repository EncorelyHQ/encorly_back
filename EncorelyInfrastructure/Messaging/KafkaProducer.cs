using Confluent.Kafka;
using EncorelyApplication.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EncorelyInfrastructure.Messaging;

public class KafkaProducer<T> : IKafkaProducer<T>, IDisposable where T : class
{
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaProducer<T>> _logger;

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer<T>> logger)
    {
        _logger = logger;
        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            Acks = Acks.All
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    public async ValueTask ProduceAsync(string topic, T message, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            await _producer.ProduceAsync(topic, new Message<Null, string> { Value = json }, cancellationToken);
            _logger.LogInformation("Message sent to topic {Topic}", topic);
        }
        catch (ProduceException<Null, string> e)
        {
            _logger.LogError(e, "Error producing message to topic {Topic}", topic);
            throw;
        }
    }

    public void Dispose()
    {
        _producer.Flush();
        _producer.Dispose();
    }
}
