using Microsoft.AspNetCore.SignalR;

namespace EncorelyApi.Hubs;

public class NotificationHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    }
}

public interface INotificationClient
{
    Task NotifyMatchFound(Guid userId, Guid matchId, double affinity);
}
