using EncorelyDomain.Entities;

namespace EncorelyApplication.Interfaces;

public interface IVenueService
{
    Task<VenueRoom> CreateVenueRoomAsync(string eventId, string name, TimeSpan duration, CancellationToken ct = default);
    Task<IEnumerable<VenueMessage>> GetActiveMessagesAsync(Guid roomId, CancellationToken ct = default);
    Task<VenueMessage> PostMessageAsync(Guid roomId, Guid senderId, string content, CancellationToken ct = default);
    Task<bool> ModerateMessageAsync(Guid messageId, string reason, CancellationToken ct = default);
}
