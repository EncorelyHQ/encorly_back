using EncorelyModels;
using EncorelyDomain.Events;

namespace EncorelyApplication.Interfaces;

public interface ISwipeService
{
    Task RegisterSwipeAsync(Guid userId, string trackId, SwipeDirection direction, CancellationToken ct = default);
    Task<object> GetNextTrackAsync(Guid userId, CancellationToken ct = default);
}
