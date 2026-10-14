namespace Encorely.Api.Features.Music;

public sealed class Genre
{
    public int Id { get; set; }
    /// <summary>Identificador estable que viaja en la API (p. ej. "vallenato").</summary>
    public required string Slug { get; set; }
    public required string Name { get; set; }
}

public sealed class Song
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Title { get; set; }
    public required string Artist { get; set; }
    public int GenreId { get; set; }
    public Genre? Genre { get; set; }
    public required string CoverUrl { get; set; }
    /// <summary>Preview de 30 s de iTunes Search. Puede faltar.</summary>
    public string? PreviewUrl { get; set; }
    public long? ITunesTrackId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum SwipeDirection { Like, Dislike }

/// <summary>Un swipe por (usuario, canción): repetir la canción no suma.</summary>
public sealed class Swipe
{
    public Guid UserId { get; set; }
    public Guid SongId { get; set; }
    public SwipeDirection Direction { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Componente del ADN musical por género. El peso efectivo se calcula como
/// Likes − 0,5·Dislikes + ImportedWeight (+ bono si el género está declarado).
/// Se actualiza con upsert atómico en la misma transacción del swipe.
/// </summary>
public sealed class UserGenreScore
{
    public Guid UserId { get; set; }
    public int GenreId { get; set; }
    public int Likes { get; set; }
    public int Dislikes { get; set; }
    /// <summary>Aporte del conector Spotify (se reemplaza en cada importación).</summary>
    public double ImportedWeight { get; set; }
}
