namespace EncorelyApplication.Interfaces;

public interface IMatchService
{
    Task<IEnumerable<object>> GetPendingMatchesAsync(Guid userId, CancellationToken ct = default);
    Task<Guid> AcceptMatchAsync(Guid userId, Guid matchId, CancellationToken ct = default);
    Task<IEnumerable<object>> GetChatMessagesAsync(Guid roomId, CancellationToken ct = default);
    Task<object> SendMessageAsync(Guid matchId, Guid senderId, string content, CancellationToken ct = default);
}
