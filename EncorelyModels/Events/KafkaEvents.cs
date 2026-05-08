namespace EncorelyDomain.Events;

public record KafkaEvent(string EventType, DateTime Timestamp);

public record UserSyncEvent(Guid UserId, string SpotifyToken, DateTime Timestamp);

public record SwipeRegisteredEvent(Guid UserId, string TrackId, string Direction) : KafkaEvent("SwipeRegistered", DateTime.UtcNow);

public record DnaCompletedEvent(Guid UserId, DateTime Timestamp) : KafkaEvent("DnaCompleted", Timestamp);

public record UserSyncedEvent(
    Guid UserId, 
    string SpotifyId, 
    List<string> TopArtists, 
    List<string> TopGenres
) : KafkaEvent("UserSynced", DateTime.UtcNow);
