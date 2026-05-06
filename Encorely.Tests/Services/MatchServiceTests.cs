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

public class MatchServiceTests
{
    private readonly IMatchService _sut;
    private readonly IMatchNotificationService _notificationService = Substitute.For<IMatchNotificationService>();
    private readonly IMatchQueries _matchQueries = Substitute.For<IMatchQueries>();
    private readonly IMatchRepository _matchRepository = Substitute.For<IMatchRepository>();
    private readonly IMessageQueries _messageQueries = Substitute.For<IMessageQueries>();
    private readonly IMessageRepository _messageRepository = Substitute.For<IMessageRepository>();
    private readonly IEventProducer<MatchConvertedToChatEvent> _analyticsProducer = Substitute.For<IEventProducer<MatchConvertedToChatEvent>>();

    public MatchServiceTests()
    {
        _sut = new MatchService(
            _notificationService, 
            _matchQueries, 
            _matchRepository, 
            _messageQueries, 
            _messageRepository, 
            _analyticsProducer);
    }

    [Fact]
    public async Task AcceptMatchAsync_ShouldNotifyAndCreateSystemMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var match = new Match { Id = matchId, UserId1 = userId, UserId2 = Guid.NewGuid(), AffinityScore = 0.9 };
        
        _matchQueries.GetByIdAsync(matchId).Returns(match);

        // Act
        var result = await _sut.AcceptMatchAsync(userId, matchId);

        // Assert
        result.Should().Be(matchId);
        await _notificationService.Received(1).NotifyMatchFoundAsync(userId, matchId, match.AffinityScore, Arg.Any<CancellationToken>());
        
        await _messageRepository.Received(1).CreateAsync(Arg.Is<Message>(m => m.MatchId == matchId && m.SenderId == Guid.Empty));
    }

    [Fact]
    public async Task SendMessageAsync_ShouldProduceAnalytics_OnFirstMessage()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var content = "Hola!";
        
        _messageQueries.GetByMatchIdAsync(matchId).Returns(new List<Message>());

        // Act
        await _sut.SendMessageAsync(matchId, senderId, content);

        // Assert
        await _analyticsProducer.Received(1).ProduceAsync(KafkaTopics.MatchConvertedToChat, Arg.Any<MatchConvertedToChatEvent>(), Arg.Any<CancellationToken>());
    }
}
