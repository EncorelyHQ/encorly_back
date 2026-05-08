using EncorelyModels;

namespace EncorelyRepository.Interfaces;

public interface IMatchRepository
{
    Task<Guid> CreateAsync(Match match);
    Task<bool> DeleteAsync(Guid id);
}
