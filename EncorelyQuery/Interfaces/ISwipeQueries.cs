using EncorelyModels;

namespace EncorelyQuery.Interfaces;

public interface ISwipeQueries
{
    Task<Swipe?> GetByIdAsync(Guid id);
    Task<IEnumerable<Swipe>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<Swipe>> GetSwipesForUserAndTrackAsync(Guid userId, string trackId);
}
