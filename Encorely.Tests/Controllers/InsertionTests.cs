using EncorelyApi.Controllers;
using EncorelyApplication.Interfaces;
using EncorelyDomain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Encorely.Tests.Controllers;

public class InsertionTests
{
    // 1. Swipes Controller
    [Fact]
    public async Task SwipesController_RegisterSwipe_ShouldReturnAccepted()
    {
        // Arrange
        var swipeService = Substitute.For<ISwipeService>();
        var sut = new SwipesController(swipeService);
        var request = new SwipeRequest(Guid.NewGuid(), "track-123", SwipeDirection.Like);

        // Act
        var result = await sut.RegisterSwipe(request, default);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
        await swipeService.Received(1).RegisterSwipeAsync(request.UserId, request.TrackId, request.Direction, Arg.Any<CancellationToken>());
    }

    // 2. Matches Controller
    [Fact]
    public async Task MatchesController_AcceptMatch_ShouldReturnOk()
    {
        // Arrange
        var matchService = Substitute.For<IMatchService>();
        var sut = new MatchesController(matchService);
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        matchService.AcceptMatchAsync(userId, matchId, Arg.Any<CancellationToken>()).Returns(roomId);

        // Act
        var result = await sut.AcceptMatch(matchId, userId, default);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.ToString().Should().Contain(roomId.ToString());
    }

    // 3. Venue Controller
    [Fact]
    public async Task VenueController_CreateRoom_ShouldReturnOk()
    {
        // Arrange
        var venueService = Substitute.For<IVenueService>();
        var hubContext = Substitute.For<Microsoft.AspNetCore.SignalR.IHubContext<EncorelyApi.Hubs.VenueHub>>();
        var sut = new VenueController(venueService, hubContext);
        var eventId = "event-456";
        var room = new VenueRoom { Id = Guid.NewGuid(), Name = "Moshpit", EventId = eventId };
        venueService.CreateVenueRoomAsync(eventId, "Moshpit", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(room);

        // Act
        var result = await sut.CreateRoom(eventId, "Moshpit", 4, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
