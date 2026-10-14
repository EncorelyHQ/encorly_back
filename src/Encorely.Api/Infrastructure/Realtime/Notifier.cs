using Encorely.Api.Infrastructure.Push;
using Microsoft.AspNetCore.SignalR;

namespace Encorely.Api.Infrastructure.Realtime;

/// <summary>
/// Fachada única de avisos (hub + push). Los fallos de entrega se registran y no rompen la operación:
/// el dato ya quedó persistido y el cliente lo recupera por REST.
/// </summary>
public sealed class Notifier(IHubContext<ChatHub, IChatClient> hub, IPushSender push, HubConnections connections, ILogger<Notifier> logger)
{
    /// <summary>Corta las conexiones en tiempo real de una cuenta suspendida o borrada.</summary>
    public void Disconnect(Guid userId) => connections.Abort(userId);

    public async Task MessageAvailableAsync(Guid recipientId, MessageAvailableEvent e)
    {
        await Safe(() => hub.Clients.User(recipientId.ToString()).MessageAvailable(e));
        await Safe(() => push.SendAsync(recipientId, new PushNotification(PushKind.NewMessage, e.MatchId)));
    }

    public async Task MatchCreatedAsync(Guid matchId, params Guid[] userIds)
    {
        foreach (var userId in userIds)
        {
            await Safe(() => hub.Clients.User(userId.ToString()).MatchCreated(new MatchCreatedEvent(matchId)));
            await Safe(() => push.SendAsync(userId, new PushNotification(PushKind.NewMatch, matchId)));
        }
    }

    public Task MatchClosedAsync(Guid matchId, params Guid[] userIds)
        => Safe(() => hub.Clients.Users(userIds.Select(u => u.ToString()).ToList()).MatchClosed(new MatchClosedEvent(matchId)));

    public Task PeerKeyChangedAsync(Guid recipientId, Guid matchId, Guid changedUserId)
        => Safe(() => hub.Clients.User(recipientId.ToString()).PeerKeyChanged(new PeerKeyChangedEvent(matchId, changedUserId)));

    private async Task Safe(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { logger.LogWarning(ex, "No se pudo entregar un aviso en tiempo real/push"); }
    }
}
