using EncorelyApplication.Interfaces;
using EncorelyApplication.Services;
using EncorelyModels;
using EncorelyDomain.Events;
using EncorelyQuery.Interfaces;
using EncorelyRepository.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Encorely.Tests.Services;

public class VenueServiceTests
{
    private readonly VenueService _sut;
    private readonly IVenueRoomQueries _roomQueries = Substitute.For<IVenueRoomQueries>();
    private readonly IVenueRoomRepository _roomRepository = Substitute.For<IVenueRoomRepository>();
    private readonly IVenueMessageQueries _messageQueries = Substitute.For<IVenueMessageQueries>();
    private readonly IVenueMessageRepository _messageRepository = Substitute.For<IVenueMessageRepository>();
    private readonly IEventProducer<VenueMessageFlaggedEvent> _moderationProducer = Substitute.For<IEventProducer<VenueMessageFlaggedEvent>>();

    public VenueServiceTests()
    {
        _sut = new VenueService(
            _roomQueries, 
            _roomRepository, 
            _messageQueries, 
            _messageRepository, 
            _moderationProducer);
    }

    [Fact]
    public async Task PostMessageAsync_WithBannedKeyword_ShouldFlagMessageAndProduceEvent()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var content = "This message contains hate speech"; // 'hate' is a banned keyword

        var room = new VenueRoom { Id = roomId, EventId = "event-1", Name = "Test Room", ExpiresAt = DateTime.UtcNow.AddHours(1) };
        _roomQueries.GetByIdAsync(roomId).Returns(room);

        // Act
        var result = await _sut.PostMessageAsync(roomId, senderId, content);

        // Assert
        result.IsModerated.Should().BeTrue();
        await _moderationProducer.Received(1).ProduceAsync(Arg.Any<string>(), Arg.Any<VenueMessageFlaggedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostMessageAsync_CleanMessage_ShouldNotBeModerated()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var content = "This is a clean concert message!";

        var room = new VenueRoom { Id = roomId, EventId = "event-1", Name = "Test Room", ExpiresAt = DateTime.UtcNow.AddHours(1) };
        _roomQueries.GetByIdAsync(roomId).Returns(room);

        // Act
        var result = await _sut.PostMessageAsync(roomId, senderId, content);

        // Assert
        result.IsModerated.Should().BeFalse();
        await _moderationProducer.DidNotReceive().ProduceAsync(Arg.Any<string>(), Arg.Any<VenueMessageFlaggedEvent>(), Arg.Any<CancellationToken>());
    }
}
