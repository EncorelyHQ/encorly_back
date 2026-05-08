using EncorelyModels;

namespace EncorelyRepository.Interfaces;

public interface IVenueMessageRepository
{
    Task<Guid> CreateAsync(VenueMessage message);
    Task<bool> UpdateAsync(VenueMessage message);
}
