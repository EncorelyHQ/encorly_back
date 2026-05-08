using EncorelyModels;

namespace EncorelyRepository.Interfaces;

public interface IMusicalProfileRepository
{
    Task<Guid> CreateOrUpdateAsync(MusicalProfile profile);
}
