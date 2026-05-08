using EncorelyModels;

namespace EncorelyQuery.Interfaces;

public interface IMusicalProfileQueries
{
    Task<MusicalProfile?> GetByUserIdAsync(Guid userId);
}
