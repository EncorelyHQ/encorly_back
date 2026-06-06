using EncorelyApplication.Interfaces;
using EncorelyApplication.Services;
using EncorelyDomain.Events;
using EncorelyModels;
using EncorelyQuery.Interfaces;
using EncorelyRepository.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Encorely.Tests.Services;

public class SwipeServiceTests
{
    private readonly ISwipeService _sut;
    private readonly IUsuarioQueries _usuarioQueries = Substitute.For<IUsuarioQueries>();
    private readonly IUsuarioRepository _usuarioRepository = Substitute.For<IUsuarioRepository>();
    private readonly ISwipeRepository _swipeRepository = Substitute.For<ISwipeRepository>();
    private readonly IKafkaProducer<SwipeRegisteredEvent> _kafkaProducer = Substitute.For<IKafkaProducer<SwipeRegisteredEvent>>();
    private readonly ILogger<SwipeService> _logger = Substitute.For<ILogger<SwipeService>>();

    public SwipeServiceTests()
    {
        _swipeRepository.CreateAsync(Arg.Any<Swipe>()).Returns(Guid.NewGuid());
        _usuarioRepository.IncrementSwipeCountAsync(Arg.Any<Guid>()).Returns(1);

        _sut = new SwipeService(
            _usuarioQueries,
            _usuarioRepository,
            _swipeRepository,
            _kafkaProducer,
            _logger);
    }

    [Fact]
    public async Task RegisterSwipeAsync_WhenUserExists_ShouldCreateSwipeIncrementCountAndProduceKafkaEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string trackId = "track-abc-123";
        const SwipeDirection direction = SwipeDirection.Right;
        _usuarioQueries.GetByIdAsync(userId).Returns(new Usuario { Id = userId });

        // Act
        await _sut.RegisterSwipeAsync(userId, trackId, direction);

        // Assert
        await _swipeRepository.Received(1).CreateAsync(
            Arg.Is<Swipe>(s => s.UserId == userId && s.TrackId == trackId && s.Direction == direction));
        await _usuarioRepository.Received(1).IncrementSwipeCountAsync(userId);
        await _kafkaProducer.Received(1).ProduceAsync(
            KafkaTopics.SwipeRawEvents,
            Arg.Is<SwipeRegisteredEvent>(e => e.UserId == userId && e.TrackId == trackId && e.Direction == direction.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterSwipeAsync_WhenUserNotFound_ShouldThrowKeyNotFoundExceptionAndNotPersist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _usuarioQueries.GetByIdAsync(userId).Returns((Usuario?)null);

        // Act & Assert
        await _sut.Invoking(s => s.RegisterSwipeAsync(userId, "any-track", SwipeDirection.Left))
            .Should().ThrowAsync<KeyNotFoundException>();

        await _swipeRepository.DidNotReceive().CreateAsync(Arg.Any<Swipe>());
        await _usuarioRepository.DidNotReceive().IncrementSwipeCountAsync(Arg.Any<Guid>());
        await _kafkaProducer.DidNotReceive().ProduceAsync(
            Arg.Any<string>(), Arg.Any<SwipeRegisteredEvent>(), Arg.Any<CancellationToken>());
    }
}
