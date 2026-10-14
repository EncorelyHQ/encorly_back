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
        ("regional-mexicano", ["corrido", "corridos tumbados", "regional mexican", "regional mexicano", "mariachi", "norteno", "sierreno", "ranchera", "banda"]),
        ("rock-en-espanol", ["rock en espanol", "rock latino", "latin rock", "rock colombiano", "rock en castellano"]),
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
        {
            var text = $" {Normalize(raw)} ";
            if (Ignored.Any(i => text.Contains($" {i} ", StringComparison.Ordinal))) continue;
            foreach (var (slug, keywords) in Rules)
            {
                // Palabras completas: "rap" no debe coincidir con "trap" ni "pop" con "popular".
                if (!keywords.Any(k => text.Contains($" {k} ", StringComparison.Ordinal))) continue;
                slugs.Add(slug);
                break;
            }
        }
        return slugs;
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
