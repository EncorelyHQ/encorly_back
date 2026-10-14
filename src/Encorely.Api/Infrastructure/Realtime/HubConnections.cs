using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Encorely.Api.Infrastructure.Realtime;

/// <summary>
/// Conexiones vivas del hub por usuario. SignalR no re-autentica una conexión abierta: al suspender o borrar una
/// cuenta hay que cortarlas a mano para que deje de recibir avisos.
/// </summary>
public sealed class HubConnections
{
    private readonly ConcurrentDictionary<string, HubCallerContext> _byConnection = new();

    public void Add(HubCallerContext context) => _byConnection[context.ConnectionId] = context;

    public void Remove(string connectionId) => _byConnection.TryRemove(connectionId, out _);

    public int Abort(Guid userId)
    {
        var id = userId.ToString();
        var aborted = 0;
        foreach (var context in _byConnection.Values.Where(c => c.UserIdentifier == id))
        {
            context.Abort();
            aborted++;
        }
        return aborted;
    }
}
