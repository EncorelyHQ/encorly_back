using EncorelyApplication.Interfaces;
using EncorelyApplication.Services;
using EncorelyDomain.Entities;
using EncorelyDomain.Events;
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
    public async Task AcceptMatchAsync_ShouldNotifyAndCreateSystemMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var match = new Match { Id = matchId, UserId1 = userId, UserId2 = Guid.NewGuid(), AffinityScore = 0.9 };
        await _dbContext.Matches.AddAsync(match);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.AcceptMatchAsync(userId, matchId);

        // Assert
        result.Should().Be(matchId);
        await _notificationService.Received(1).NotifyMatchFoundAsync(userId, matchId, match.AffinityScore, Arg.Any<CancellationToken>());
        
        var message = await _dbContext.Messages.FirstOrDefaultAsync(m => m.MatchId == matchId && m.SenderId == Guid.Empty);
        message.Should().NotBeNull();
        message.Content.Should().Contain("Match creado");
    }

    [Fact]
    public async Task SendMessageAsync_ShouldProduceAnalytics_OnFirstMessage()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var content = "Hola!";

        // Act
        await _sut.SendMessageAsync(matchId, senderId, content);

        // Assert
        await _analyticsProducer.Received(1).ProduceAsync(KafkaTopics.MatchConvertedToChat, Arg.Any<MatchConvertedToChatEvent>(), Arg.Any<CancellationToken>());
    }
}
