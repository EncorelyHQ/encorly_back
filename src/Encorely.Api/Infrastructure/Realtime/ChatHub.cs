using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Encorely.Api.Infrastructure.Realtime;

public sealed record MessageAvailableEvent(Guid MatchId, Guid Id, long Seq);
public sealed record PeerKeyChangedEvent(Guid MatchId, Guid UserId);
public sealed record MatchClosedEvent(Guid MatchId);
public sealed record MatchCreatedEvent(Guid MatchId);

/// <summary>Eventos servidor → app. La fuente de verdad siempre es el REST; el hub solo avisa.</summary>
public interface IChatClient
{
    Task MessageAvailable(MessageAvailableEvent e);
    Task PeerKeyChanged(PeerKeyChangedEvent e);
    Task MatchClosed(MatchClosedEvent e);
    Task MatchCreated(MatchCreatedEvent e);
}

/// <summary>Hub autenticado y sin métodos invocables por el cliente: nadie puede emitir en nombre de otro.</summary>
[Authorize]
public sealed class ChatHub(HubConnections connections) : Hub<IChatClient>
{
    public const string Path = "/hubs/chat";

    public override Task OnConnectedAsync()
    {
        connections.Add(Context);
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        return Task.CompletedTask;
    }
}

/// <summary>Enruta Clients.User(id) por el claim <c>sub</c> del JWT.</summary>
public sealed class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.FindFirst("sub")?.Value;
}
