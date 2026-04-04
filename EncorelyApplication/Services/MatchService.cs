using EncorelyApplication.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EncorelyApplication.Services;

public class MatchService : IMatchService
{
    private readonly IMatchNotificationService _notificationService;
    private readonly IEncorelyDbContext _dbContext;
    
    public MatchService(IMatchNotificationService notificationService, IEncorelyDbContext dbContext)
    {
        _notificationService = notificationService;
        _dbContext = dbContext;
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
}
