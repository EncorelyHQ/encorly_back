using EncorelyApi.Hubs;
using EncorelyApplication.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace EncorelyApi.Services;

public class SignalRNotificationService : IMatchNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyMatchFoundAsync(Guid userId, Guid matchId, double affinityScore, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(userId.ToString()).SendAsync("NotifyMatchFound", userId, matchId, affinityScore, ct);
        await _hubContext.Clients.Group(matchId.ToString()).SendAsync("NotifyMatchFound", matchId, userId, affinityScore, ct);
    }
}
