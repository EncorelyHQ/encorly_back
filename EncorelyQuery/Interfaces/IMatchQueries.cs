using EncorelyModels;

namespace EncorelyQuery.Interfaces;

public interface IMatchQueries
{
    Task<Match?> GetByIdAsync(Guid id);
    Task<IEnumerable<Match>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<Match>> GetAllAsync();
}
