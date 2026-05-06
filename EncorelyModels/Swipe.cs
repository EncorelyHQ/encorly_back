namespace EncorelyModels;

public enum SwipeDirection
{
    Left,
    Right,
    Down
}

public class Swipe
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TrackId { get; set; } = string.Empty;
    public SwipeDirection Direction { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Usuario? Usuario { get; set; }
}
