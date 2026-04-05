using EncorelyInfrastructure.Hubs;
using EncorelyApplication.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace EncorelyApi.Services;

public class SignalRNotificationService : IMatchNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IPushNotificationService _pushService;

    public SignalRNotificationService(
        IHubContext<NotificationHub> hubContext,
        IPushNotificationService pushService)
    {
        _hubContext = hubContext;
        _pushService = pushService;
    }

    public async Task NotifyMatchFoundAsync(Guid userId, Guid matchId, double affinityScore, CancellationToken ct = default)
    {
        // Emit via WebSockets
        await _hubContext.Clients.Group(userId.ToString()).SendAsync("NotifyMatchFound", userId, matchId, affinityScore, ct);
        await _hubContext.Clients.Group(matchId.ToString()).SendAsync("NotifyMatchFound", matchId, userId, affinityScore, ct);

        // Fallback or multi-device broad push notification
        var payloadData = new Dictionary<string, string> { { "matchId", matchId.ToString() } };
        var title = "¡Encorely Match Musical!";
        var body = $"¡Has logrado un Match! Afinidad: {Math.Round(affinityScore, 2)}%";

        await _pushService.SendPushNotificationAsync(userId, title, body, payloadData, ct);
        await _pushService.SendPushNotificationAsync(matchId, title, body, payloadData, ct);
    }
}
