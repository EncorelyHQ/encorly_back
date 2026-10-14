using Encorely.Api.Features.Music;

namespace Encorely.Api.Features.Users;

public enum UserRole { User, Admin }

public enum UserStatus { Active, Banned }

public sealed class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Siempre normalizado (trim + minúsculas). Único.</summary>
    public required string Email { get; set; }
    /// <summary>null en las cuentas creadas con Spotify: no tienen contraseña y el login por correo no las acepta.</summary>
    public string? PasswordHash { get; set; }
    /// <summary>Id de usuario de Spotify vinculado (login con Spotify). Único.</summary>
    public string? SpotifyId { get; set; }
    public required string DisplayName { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? City { get; set; }
    public string? Bio { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockoutEndsAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<UserGenre> Genres { get; set; } = [];

    public bool HasPassword => PasswordHash is not null;

    public bool OnboardingCompleted => BirthDate is not null && !string.IsNullOrWhiteSpace(City) && Genres.Count > 0;

    public static int AgeOn(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;
        return age;
    }
}

/// <summary>Géneros que el usuario declara como favoritos (onboarding/perfil).</summary>
public sealed class UserGenre
{
    public Guid UserId { get; set; }
    public int GenreId { get; set; }
    public Genre? Genre { get; set; }
}

/// <summary>Aceptación versionada de términos y política de datos (prueba de consentimiento, Ley 1581).</summary>
public sealed class UserConsent
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public required string Version { get; set; }
    public DateTimeOffset AcceptedAt { get; set; } = DateTimeOffset.UtcNow;
}
