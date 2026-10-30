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

/// <summary>
/// Artista del top de Spotify del usuario, para mostrarlo en el perfil y reaccionarle. Cada importación reemplaza
/// la lista completa. <see cref="Key"/> es el nombre normalizado (<see cref="ArtistKey"/>): así se cruzan entre personas.
/// </summary>
public sealed class UserArtist
{
    public Guid UserId { get; set; }
    public required string Key { get; set; }
    public required string Name { get; set; }
    /// <summary>Posición en el top de Spotify (0 = el más escuchado).</summary>
    public int Rank { get; set; }
    /// <summary>Solo https de i.scdn.co (CDN de Spotify); cualquier otra se descarta.</summary>
    public string? ImageUrl { get; set; }
    public List<string> Genres { get; set; } = [];
    public DateTimeOffset ImportedAt { get; set; }
}

public static class ArtistKey
{
    public const int MaxLength = 100;

    // Con InvariantGlobalization, string.Normalize no descompone las tildes: se pliegan a mano las del español y vecinas.
    private const string Accented = "áàäâãåéèëêíìïîóòöôõúùüûñçý";
    private const string Plain = "aaaaaaeeeeiiiiooooouuuuncy";

    /// <summary>"Bad Bunny" y "bad bunny " dan "bad-bunny"; sin tildes ni signos. Vacío si el nombre no tiene letras ni dígitos.</summary>
    public static string Of(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var raw in name)
        {
            var c = char.ToLowerInvariant(raw);
            var i = Accented.IndexOf(c);
            if (i >= 0) c = Plain[i];
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var key = sb.ToString().Trim('-');
        return key.Length <= MaxLength ? key : key[..MaxLength].TrimEnd('-');
    }

    /// <summary>La URL de la imagen solo se guarda si es https del CDN de Spotify: no se sirven URLs arbitrarias a otros.</summary>
    public static string? SafeImageUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.Host == "i.scdn.co"
            && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) ? uri.AbsoluteUri : null;
}
