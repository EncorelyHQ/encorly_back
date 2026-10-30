using System.Collections.Concurrent;
using Encorely.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.SignalR;

namespace Encorely.Api.Infrastructure.Realtime;

/// <summary>
/// Conexiones vivas del hub por usuario. SignalR no re-autentica una conexión abierta: al suspender o borrar una
/// cuenta, o al cerrar una sesión, hay que cortarlas a mano para que dejen de recibir avisos.
/// </summary>
public sealed class HubConnections
{
    private readonly ConcurrentDictionary<string, HubCallerContext> _byConnection = new();

    public void Add(HubCallerContext context) => _byConnection[context.ConnectionId] = context;

    public void Remove(string connectionId) => _byConnection.TryRemove(connectionId, out _);

    /// <param name="sessionId">Solo las conexiones abiertas con un access token de esa sesión (claim <c>sid</c>); null = todas.</param>
    public int Abort(Guid userId, Guid? sessionId = null)
    {
        var id = userId.ToString();
        var aborted = 0;
        foreach (var context in _byConnection.Values.Where(c => c.UserIdentifier == id
                     && (sessionId is null || c.User?.SessionId() == sessionId)))
        {
            context.Abort();
            aborted++;
        }
        return aborted;
    }
}
