using Confluent.Kafka;
using EncorelyDomain.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace EncorelyWorker;

public class KafkaConsumerWorker : BackgroundService
{
    private readonly ILogger<KafkaConsumerWorker> _logger;
    private readonly IConfiguration _configuration;

    public KafkaConsumerWorker(ILogger<KafkaConsumerWorker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = "encorely-processor-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(new[] { "swipe-raw-events" });

        _logger.LogInformation("Encorely Worker started. Listening for 'swipe-raw-events'...");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var consumeResult = consumer.Consume(stoppingToken);
                var swipeEvent = JsonSerializer.Deserialize<SwipeRegisteredEvent>(consumeResult.Message.Value);
                
                if (swipeEvent != null)
                {
                    _logger.LogInformation("[WORKER] Procesando Swipe de Usuario {UserId} para Track {TrackId}", swipeEvent.UserId, swipeEvent.TrackId);
                    
                    var dnaEvent = new DnaCompletedEvent(swipeEvent.UserId, DateTime.UtcNow);
                    _logger.LogInformation("[DNA_COMPLETED] El ADN Musical del usuario {UserId} está listo.", swipeEvent.UserId);
                }
                
                await Task.Delay(100, stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            consumer.Close();
        }
    }
}
