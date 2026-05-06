using EncorelyModels;

namespace EncorelyRepository.Interfaces;

public interface ISwipeRepository
{
    Task<Guid> CreateAsync(Swipe swipe);
    Task<bool> DeleteAsync(Guid id);
}
