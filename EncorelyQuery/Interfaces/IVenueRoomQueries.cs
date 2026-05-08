using EncorelyModels;

namespace EncorelyQuery.Interfaces;

public interface IVenueRoomQueries
{
    Task<VenueRoom?> GetByIdAsync(Guid id);
    Task<IEnumerable<VenueRoom>> GetAllAsync();
    Task<IEnumerable<VenueRoom>> GetActiveRoomsAsync();
}
