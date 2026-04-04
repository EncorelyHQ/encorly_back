namespace EncorelyDomain.Events;

public static class KafkaTopics
{
    public const string UserDnaSync = "user-dna-sync";
    public const string SwipeRawEvents = "swipe-raw-events";
    public const string DnaCompleted = "dna-completed";
    public const string MatchFound = "match-found";
    public const string MatchConvertedToChat = "match-converted-chat"; // Tarea 77: analytics
}
