namespace Encorely.Api.Features.Music;

/// <summary>
/// Fórmula única del ADN musical (vector de géneros). La usan /me/dna, el radar (replicada en SQL con estas
/// mismas constantes) y los géneros compartidos de los matches. Cambiarla aquí la cambia en todas partes.
/// </summary>
public static class Dna
{
    public const double LikeWeight = 1.0;
    public const double DislikePenalty = 0.5;
    /// <summary>Bono por género declarado en el perfil: da señal desde el onboarding, antes de los swipes.</summary>
    public const double DeclaredBonus = 2.0;

    /// <summary>Peso crudo de un género (nunca negativo).</summary>
    public static double RawWeight(int likes, int dislikes, double imported, bool declared)
        => Math.Max(0, likes * LikeWeight - dislikes * DislikePenalty + imported + (declared ? DeclaredBonus : 0));

    /// <summary>Vector crudo por genreId a partir de los scores y los géneros declarados.</summary>
    public static Dictionary<int, double> Vector(IEnumerable<UserGenreScore> scores, IEnumerable<int> declaredGenreIds)
    {
        var declared = declaredGenreIds.ToHashSet();
        var vector = new Dictionary<int, double>();
        foreach (var s in scores)
        {
            var w = RawWeight(s.Likes, s.Dislikes, s.ImportedWeight, declared.Remove(s.GenreId));
            if (w > 0) vector[s.GenreId] = w;
        }
        foreach (var genreId in declared) vector[genreId] = RawWeight(0, 0, 0, true);
        return vector;
    }

    /// <summary>Pesos normalizados que suman 1 (vacío si no hay señal).</summary>
    public static Dictionary<int, double> Normalize(IReadOnlyDictionary<int, double> vector)
    {
        var total = vector.Values.Sum();
        return total <= 0 ? [] : vector.ToDictionary(kv => kv.Key, kv => kv.Value / total);
    }

    /// <summary>Afinidad 0–100: coseno entre vectores crudos (no negativos), redondeado.</summary>
    public static int Affinity(IReadOnlyDictionary<int, double> a, IReadOnlyDictionary<int, double> b)
    {
        double dot = 0, na = 0, nb = 0;
        foreach (var (g, w) in a) { na += w * w; if (b.TryGetValue(g, out var wb)) dot += w * wb; }
        foreach (var w in b.Values) nb += w * w;
        return na == 0 || nb == 0 ? 0 : (int)Math.Round(100 * dot / (Math.Sqrt(na) * Math.Sqrt(nb)), MidpointRounding.AwayFromZero);
    }

    /// <summary>Hasta <paramref name="max"/> géneros con peso en ambos, ordenados por el menor de los dos pesos normalizados.</summary>
    public static IReadOnlyList<int> Shared(IReadOnlyDictionary<int, double> a, IReadOnlyDictionary<int, double> b, int max = 3)
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        return na.Where(kv => nb.ContainsKey(kv.Key))
            .OrderByDescending(kv => Math.Min(kv.Value, nb[kv.Key]))
            .ThenBy(kv => kv.Key)
            .Take(max)
            .Select(kv => kv.Key)
            .ToList();
    }
}
