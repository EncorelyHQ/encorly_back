using EncorelyWorker;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Encorely.Tests.Services;

public class KafkaConsumerWorkerTests
{
    private static KafkaConsumerWorker BuildWorker(IServiceScopeFactory? scopeFactory = null)
    {
        var config = Substitute.For<IConfiguration>();
        config["Kafka:BootstrapServers"].Returns((string?)null); // fallback → localhost:9092
        return new KafkaConsumerWorker(
            Substitute.For<ILogger<KafkaConsumerWorker>>(),
            config,
            scopeFactory ?? Substitute.For<IServiceScopeFactory>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledBeforeStart_ShouldCompleteWithoutException()
    {
        // Arrange
        // Token pre-cancelado: el while(!stoppingToken.IsCancellationRequested) nunca entra
        // → consumer.Consume() nunca se llama → no hay conexión a Kafka → no hay timeout en Close()
        var worker = BuildWorker();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () =>
        {
            await worker.StartAsync(cts.Token);
            await worker.StopAsync(CancellationToken.None);
        };
        await act.Should().NotThrowAsync();
    }
}
