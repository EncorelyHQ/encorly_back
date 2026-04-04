using EncorelyApplication.Interfaces;
using EncorelyDomain.Events;
using Microsoft.EntityFrameworkCore;

namespace EncorelyApplication.Services;

public class MatchService : IMatchService
{
    private readonly IMatchNotificationService _notificationService;
    private readonly IEncorelyDbContext _dbContext;
    private readonly IEventProducer<MatchConvertedToChatEvent> _analyticsProducer;

    public MatchService(
        IMatchNotificationService notificationService,
        IEncorelyDbContext dbContext,
        IEventProducer<MatchConvertedToChatEvent> analyticsProducer)
    {
        _notificationService = notificationService;
        _dbContext = dbContext;
        _analyticsProducer = analyticsProducer;
    }

    // Persistence via Entity Framework Core
    public async Task<IEnumerable<object>> GetPendingMatchesAsync(Guid userId, CancellationToken ct = default)
    {
        var matches = await _dbContext.Matches
            .Where(m => m.UserId1 == userId || m.UserId2 == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new { MatchId = m.Id, DisplayName = "Match " + m.Id.ToString().Substring(0, 4), Compatibility = m.AffinityScore })
            .ToListAsync(ct);

        return matches;
    }

    public async Task<Guid> AcceptMatchAsync(Guid userId, Guid matchId, CancellationToken ct = default)
    {
        var match = await _dbContext.Matches.FindAsync(new object[] { matchId }, ct);
        if (match == null) throw new Exception("Match not found");
        
        var roomId = match.Id;
        
        // Tarea 3: Real-Time Notification via SignalR
        await _notificationService.NotifyMatchFoundAsync(userId, matchId, match.AffinityScore, ct);

        var welcomeMessage = new EncorelyDomain.Entities.Message
        {
            Id = Guid.NewGuid(),
            MatchId = roomId,
            SenderId = Guid.Empty, // System message
            Content = "¡Match creado! Ya pueden chatear.",
            Timestamp = DateTime.UtcNow
        };

        await _dbContext.Messages.AddAsync(welcomeMessage, ct);
        await _dbContext.SaveChangesAsync(ct);

        return roomId;
    }

    public async Task<IEnumerable<object>> GetChatMessagesAsync(Guid roomId, CancellationToken ct = default)
    {
        var messages = await _dbContext.Messages
            .Where(m => m.MatchId == roomId)
            .OrderBy(m => m.Timestamp)
            .Select(m => new { SenderId = m.SenderId, Content = m.Content, Timestamp = m.Timestamp })
            .ToListAsync(ct);

        return messages;
    }

    // Tarea 77: Match-to-Chat Conversion Analytics
    public async Task<object> SendMessageAsync(Guid matchId, Guid senderId, string content, CancellationToken ct = default)
    {
        var isFirstMessage = !await _dbContext.Messages
            .AnyAsync(m => m.MatchId == matchId && m.SenderId != Guid.Empty, ct);

        var message = new EncorelyDomain.Entities.Message
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            SenderId = senderId,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        await _dbContext.Messages.AddAsync(message, ct);
        await _dbContext.SaveChangesAsync(ct);

        if (isFirstMessage)
        {
            var analyticsEvent = new MatchConvertedToChatEvent(matchId, senderId, DateTime.UtcNow);
            await _analyticsProducer.ProduceAsync(KafkaTopics.MatchConvertedToChat, analyticsEvent, ct);
        }

        return new { message.Id, message.Content, message.Timestamp };
    }
}
