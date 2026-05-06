namespace EncorelyDomain.Events;

public record MatchConvertedToChatEvent(
    Guid MatchId,
    Guid InitiatorUserId,
    DateTime ConvertedAt
);
