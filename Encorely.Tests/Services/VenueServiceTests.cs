using EncorelyApplication.Interfaces;
using EncorelyApplication.Services;
using EncorelyDomain.Entities;
using EncorelyDomain.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Encorely.Tests.Services;

public class VenueServiceTests
{
    private readonly VenueService _sut;
    private readonly IEncorelyDbContext _dbContext;
    private readonly IEventProducer<VenueMessageFlaggedEvent> _moderationProducer = Substitute.For<IEventProducer<VenueMessageFlaggedEvent>>();

    public VenueServiceTests()
    {
        // Setup In-Memory Database for testing EF Core logic
        var options = new DbContextOptionsBuilder<MockDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new MockDbContext(options);
        _sut = new VenueService(_dbContext, _moderationProducer);
    }

    [Fact]
    public async Task PostMessageAsync_WithBannedKeyword_ShouldFlagMessageAndProduceEvent()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var content = "This message contains hate speech"; // 'hate' is a banned keyword

        var room = new VenueRoom { Id = roomId, EventId = "event-1", Name = "Test Room", ExpiresAt = DateTime.UtcNow.AddHours(1) };
        await _dbContext.VenueRooms.AddAsync(room);
        await _dbContext.SaveChangesAsync();

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
        await _dbContext.VenueRooms.AddAsync(room);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.PostMessageAsync(roomId, senderId, content);

        // Assert
        result.IsModerated.Should().BeFalse();
        await _moderationProducer.DidNotReceive().ProduceAsync(Arg.Any<string>(), Arg.Any<VenueMessageFlaggedEvent>(), Arg.Any<CancellationToken>());
    }
}

// Minimal MockDbContext for In-Memory testing
public class MockDbContext : DbContext, IEncorelyDbContext
{
    public MockDbContext(DbContextOptions<MockDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Swipe> Swipes { get; set; }
    public DbSet<MusicalProfile> MusicalProfiles { get; set; }
    public DbSet<Match> Matches { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<VenueRoom> VenueRooms { get; set; }
    public DbSet<VenueMessage> VenueMessages { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }
}
