using EncorelyModels;

namespace EncorelyQuery.Interfaces;

public interface IVenueMessageQueries
{
    Task<VenueMessage?> GetByIdAsync(Guid id);
    Task<IEnumerable<VenueMessage>> GetByRoomIdAsync(Guid roomId);
}
