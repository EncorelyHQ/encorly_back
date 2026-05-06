using EncorelyApplication.Interfaces;
using EncorelyDomain.Events;
using EncorelyQuery.Interfaces;
using EncorelyRepository.Interfaces;
using EncorelyModels;

namespace EncorelyApplication.Services;

public class MatchService : IMatchService
{
    private readonly IMatchNotificationService _notificationService;
    private readonly IMatchQueries _matchQueries;
    private readonly IMatchRepository _matchRepository;
    private readonly IMessageQueries _messageQueries;
    private readonly IMessageRepository _messageRepository;
    private readonly IEventProducer<MatchConvertedToChatEvent> _analyticsProducer;

    public MatchService(
        IMatchNotificationService notificationService,
        IMatchQueries matchQueries,
        IMatchRepository matchRepository,
        IMessageQueries messageQueries,
        IMessageRepository messageRepository,
        IEventProducer<MatchConvertedToChatEvent> analyticsProducer)
    {
        _notificationService = notificationService;
        _matchQueries = matchQueries;
        _matchRepository = matchRepository;
        _messageQueries = messageQueries;
        _messageRepository = messageRepository;
        _analyticsProducer = analyticsProducer;
    }

    public async Task<IEnumerable<object>> GetPendingMatchesAsync(Guid userId, CancellationToken ct = default)
    {
        var allMatches = await _matchQueries.GetAllAsync();
        var matches = allMatches
            .Where(m => m.UserId1 == userId || m.UserId2 == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new { MatchId = m.Id, DisplayName = "Match " + m.Id.ToString().Substring(0, 4), Compatibility = m.AffinityScore });

        return matches;
    }

    public async Task<Guid> AcceptMatchAsync(Guid userId, Guid matchId, CancellationToken ct = default)
    {
        var match = await _matchQueries.GetByIdAsync(matchId);
        if (match == null) throw new Exception("Match not found");
        
        var roomId = match.Id;
        
        await _notificationService.NotifyMatchFoundAsync(userId, matchId, match.AffinityScore, ct);

        var welcomeMessage = new EncorelyModels.Message
        {
            Id = Guid.NewGuid(),
            MatchId = roomId,
            SenderId = Guid.Empty, // System message
            Content = "¡Match creado! Ya pueden chatear.",
            Timestamp = DateTime.UtcNow
        };

        await _messageRepository.CreateAsync(welcomeMessage);

        return roomId;
    }

    public async Task<IEnumerable<object>> GetChatMessagesAsync(Guid roomId, CancellationToken ct = default)
    {
        var messages = await _messageQueries.GetByMatchIdAsync(roomId);
        return messages.OrderBy(m => m.Timestamp).Select(m => new { SenderId = m.SenderId, Content = m.Content, Timestamp = m.Timestamp });
    }

    public async Task<object> SendMessageAsync(Guid matchId, Guid senderId, string content, CancellationToken ct = default)
    {
        var messages = await _messageQueries.GetByMatchIdAsync(matchId);
        var isFirstMessage = !messages.Any(m => m.SenderId != Guid.Empty);

        var message = new EncorelyModels.Message
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            SenderId = senderId,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        await _messageRepository.CreateAsync(message);

        if (isFirstMessage)
        {
            var analyticsEvent = new MatchConvertedToChatEvent(matchId, senderId, DateTime.UtcNow);
            await _analyticsProducer.ProduceAsync(KafkaTopics.MatchConvertedToChat, analyticsEvent, ct);
        }

        return new { message.Id, message.Content, Timestamp = message.Timestamp };
    }
}
