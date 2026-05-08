namespace EncorelyModels;

/// <summary>Sala temporal de chat asociada a un evento musical en vivo.</summary>
public class VenueRoom
{
    public Guid Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; } // Temporal: expira cuando termina el evento
    public bool IsActive => DateTime.UtcNow < ExpiresAt;
    public ICollection<VenueMessage> Messages { get; set; } = new List<VenueMessage>();
}

public class VenueMessage
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsModerated { get; set; } = false; // Tarea 79: Moderation flag
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
