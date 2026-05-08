namespace EncorelyDomain.Events;

public record VenueMessageFlaggedEvent(
    Guid RoomId,
    Guid MessageId,
    Guid SenderId,
    string Reason,
    DateTime FlaggedAt
);
