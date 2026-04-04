using Microsoft.AspNetCore.SignalR;

namespace EncorelyApi.Hubs;

/// <summary>SignalR Hub para las salas grupales temporales de un venue musical.</summary>
public class VenueHub : Hub
{
    public async Task JoinVenueRoom(string roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"venue_{roomId}");
        await Clients.Group($"venue_{roomId}").SendAsync("UserJoined", Context.ConnectionId);
    }

    public async Task LeaveVenueRoom(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"venue_{roomId}");
        await Clients.Group($"venue_{roomId}").SendAsync("UserLeft", Context.ConnectionId);
    }

    public async Task SendVenueMessage(string roomId, string userId, string message)
    {
        await Clients.Group($"venue_{roomId}").SendAsync("ReceiveVenueMessage", userId, message, DateTime.UtcNow);
    }
}
