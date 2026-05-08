namespace EncorelyModels;

public enum AuthProvider
{
    Spotify,
    Google,
    Custom
}

public enum ConcertMood
{
    Moshpit,
    Chill,
    VIP
}

public class Usuario
{
    public Guid Id { get; set; }
    public string SpotifyId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public AuthProvider Provider { get; set; } = AuthProvider.Custom;
    public string? PasswordHash { get; set; }
    public int SwipeCount { get; set; } = 0;
    public ConcertMood Mood { get; set; } = ConcertMood.Chill;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }

    // Navigation property
    public ICollection<Swipe> UsuarioSwipes { get; set; } = new List<Swipe>();
}
