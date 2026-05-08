namespace EncorelyApplication.Interfaces;

public interface IMatchNotificationService
{
    Task NotifyMatchFoundAsync(Guid userId, Guid matchId, double affinityScore, CancellationToken ct = default);
}
