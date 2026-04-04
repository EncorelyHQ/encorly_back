using EncorelyApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using EncorelyApplication.Interfaces;

namespace EncorelyApplication.Services;

public class MatchService : IMatchService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    
    public MatchService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    // In-Memory Chat Storage (Tarea 2)
    private static readonly Dictionary<Guid, List<object>> _chatMemory = new();

    public async Task<IEnumerable<object>> GetPendingMatchesAsync(Guid userId, CancellationToken ct = default)
    {
        return await Task.FromResult(new List<object>
        {
            new { MatchId = Guid.NewGuid(), DisplayName = "Fan de Arctic Monkeys", Compatibility = 0.92 },
            new { MatchId = Guid.NewGuid(), DisplayName = "Rockero 80s", Compatibility = 0.85 }
        });
    }

    public async Task<Guid> AcceptMatchAsync(Guid userId, Guid matchId, CancellationToken ct = default)
    {
        var roomId = Guid.NewGuid();
        
        // Tarea 3: Real-Time Notification via SignalR
        await _hubContext.Clients.Group(userId.ToString()).SendAsync("NotifyMatchFound", userId, matchId, 0.95);
        await _hubContext.Clients.Group(matchId.ToString()).SendAsync("NotifyMatchFound", matchId, userId, 0.95);

        _chatMemory[roomId] = new List<object>
        {
            new { SenderId = Guid.Empty, Content = "¡Match creado! Ya pueden chatear.", Timestamp = DateTime.UtcNow }
        };
        return await Task.FromResult(roomId);
    }

    public async Task<IEnumerable<object>> GetChatMessagesAsync(Guid roomId, CancellationToken ct = default)
    {
        if (_chatMemory.TryGetValue(roomId, out var messages))
        {
            return await Task.FromResult(messages);
        }
        return Enumerable.Empty<object>();
    }
}
