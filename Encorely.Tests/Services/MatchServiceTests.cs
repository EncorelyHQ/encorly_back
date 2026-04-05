using EncorelyApplication.Interfaces;
using EncorelyApplication.Services;
using EncorelyDomain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Encorely.Tests.Services;

public class MatchServiceTests
{
    private readonly IMatchService _sut;
    private readonly IEncorelyDbContext _dbContext;
    private readonly IMatchNotificationService _notificationService = Substitute.For<IMatchNotificationService>();
    private readonly IEventProducer<MatchConvertedToChatEvent> _analyticsProducer = Substitute.For<IEventProducer<MatchConvertedToChatEvent>>();

    public MatchServiceTests()
    {
        var options = new DbContextOptionsBuilder<MockDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new MockDbContext(options);
        _sut = new MatchService(_notificationService, _dbContext, _analyticsProducer);
    }

    [Fact]
    public async Task CreateMatchAsync_ShouldPersistMatchAndNotify()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var affinity = 0.85;

        // Act
        var result = await _sut.CreateMatchAsync(userId1, userId2, affinity);

        // Assert
        result.Should().NotBeNull();
        result.AffinityScore.Should().Be(affinity);
        
        var persisted = await _dbContext.Matches.FirstOrDefaultAsync(m => m.Id == result.Id);
        persisted.Should().NotBeNull();
        
        await _notificationService.Received(1).NotifyMatchAsync(userId1, userId2, affinity);
    }
}
