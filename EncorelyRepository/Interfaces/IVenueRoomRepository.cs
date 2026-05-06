using EncorelyModels;

namespace EncorelyRepository.Interfaces;

public interface IVenueRoomRepository
{
    Task<Guid> CreateAsync(VenueRoom room);
    Task<bool> UpdateAsync(VenueRoom room);
    Task<bool> DeleteAsync(Guid id);
}
