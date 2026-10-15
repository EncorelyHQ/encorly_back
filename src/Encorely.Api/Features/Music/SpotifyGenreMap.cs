using System.Text;

namespace Encorely.Api.Features.Music;

/// <summary>
/// Traduce los géneros de Spotify (texto libre, en inglés o español) a nuestros slugs. Las reglas se evalúan en orden:
/// las específicas van antes que las genéricas ("salsa choke" es champeta, no salsa). Lo que no mapea se ignora.
/// </summary>
public static class SpotifyGenreMap
{
    private static readonly (string Slug, string[] Keywords)[] Rules =
    [
        ("champeta", ["champeta", "salsa choke", "terapia", "picoteo"]),
        ("trap-latino", ["latin trap", "trap latino", "trap latina", "trap"]),
        ("reggaeton", ["reggaeton", "urbano latino", "dembow", "perreo", "latin urban"]),
        ("vallenato", ["vallenato", "musica vallenata"]),
        ("salsa", ["salsa"]),
        ("cumbia", ["cumbia"]),
        ("bachata", ["bachata"]),
        ("musica-del-pacifico", ["currulao", "musica del pacifico", "pacific colombiano", "afrocolombian", "marimba"]),
        ("popular", ["musica popular colombiana", "musica popular", "popular colombiana", "carrilera", "musica guasca"]),
        ("regional-mexicano", ["corrido", "corridos", "corridos tumbados", "corridos belicos", "regional mexican", "regional mexicano", "mariachi", "norteno", "sierreno", "ranchera", "banda"]),
        // Antes de rock y electrónica: "pop rock" y "dance pop" son pop, no rock ni electrónica.
        ("pop-latino", ["dance pop", "pop rock"]),
        // "rock" a secas es la última palabra clave del rock: no hay otro slug de rock en el catálogo.
        ("rock-en-espanol", ["rock en espanol", "rock latino", "latin rock", "rock colombiano", "colombian rock", "rock en castellano",
            "alternative rock", "rock alternativo", "rock"]),
        ("indie-alternativo", ["latin alternative", "indie colombiano", "colombian indie", "indie latino", "indie", "alternative latino", "latin indie", "folk colombiano"]),
        ("electronica", ["edm", "electronica", "electro latino", "house", "techno", "tropical house", "dance"]),
        ("hip-hop", ["rap latina", "rap latino", "latin hip hop", "hip hop", "rap"]),
        ("pop-latino", ["colombian pop", "latin pop", "pop latino", "pop colombiano", "pop en espanol", "pop"]),
    ];

    private static readonly string[] Ignored = ["k pop", "j pop"];

    /// <summary>Slugs a los que mapean los géneros de un artista (sin repetidos).</summary>
    public static IReadOnlySet<string> Map(IEnumerable<string> spotifyGenres)
    {
        var slugs = new HashSet<string>();
        foreach (var raw in spotifyGenres)
            if (SlugOf(raw) is { } slug) slugs.Add(slug);
        return slugs;
    }

    /// <summary>
    /// Slug de un género de Spotify, o null si no mapea a ninguno (incluye los que se ignoran a propósito, como k-pop).
    /// </summary>
    public static string? SlugOf(string spotifyGenre)
    {
        var text = $" {Normalize(spotifyGenre)} ";
        if (Ignored.Any(i => text.Contains($" {i} ", StringComparison.Ordinal))) return null;
        foreach (var (slug, keywords) in Rules)
            // Palabras completas: "rap" no debe coincidir con "trap" ni "pop" con "popular".
            if (keywords.Any(k => text.Contains($" {k} ", StringComparison.Ordinal))) return slug;
        return null;
    }

    /// <summary>Minúsculas, sin acentos ni puntuación, espacios colapsados.</summary>
    public static string Normalize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var raw in value)
        {
            // Tabla propia: con InvariantGlobalization (ver Directory.Build.props) string.Normalize no descompone acentos.
            var c = char.ToLowerInvariant(raw) switch
            {
                'á' or 'à' or 'ä' or 'â' => 'a',
                'é' or 'è' or 'ë' or 'ê' => 'e',
                'í' or 'ì' or 'ï' or 'î' => 'i',
                'ó' or 'ò' or 'ö' or 'ô' => 'o',
                'ú' or 'ù' or 'ü' or 'û' => 'u',
                'ñ' => 'n',
                var other => other,
            };
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
