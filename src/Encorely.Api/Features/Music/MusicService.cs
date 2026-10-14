using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Music;

public sealed class MusicService(AppDbContext db, IOptions<MatchingOptions> matching, TimeProvider clock)
{
    private const int MaxLimit = 50;
    /// <summary>Parte del feed que sale de géneros afines; el resto es exploración.</summary>
    private const double AffineShare = 0.6;
    private const int AffineGenres = 5;
    /// <summary>Tope del aporte total de una importación de Spotify, para que no tape a los swipes.</summary>
    private const double MaxImportedTotal = 10;
    private const double RankDecay = 0.1;

    public async Task<IReadOnlyList<GenreDto>> GetGenresAsync(CancellationToken ct)
        => await db.Genres.AsNoTracking().OrderBy(g => g.Name).Select(g => new GenreDto(g.Id, g.Slug, g.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<SongDto>> GetFeedAsync(Guid userId, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > MaxLimit)
            throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "El límite debe estar entre 1 y 50.");

        var vector = await LoadVectorAsync(userId, ct);
        var affine = vector.OrderByDescending(kv => kv.Value).Take(AffineGenres).Select(kv => kv.Key).ToList();

        IQueryable<Song> unrated = db.Songs.AsNoTracking().Include(s => s.Genre)
            .Where(s => s.IsActive && !db.Swipes.Any(w => w.UserId == userId && w.SongId == s.Id));

        var affineWanted = (int)Math.Ceiling(limit * AffineShare);
        var picked = await unrated.Where(s => affine.Contains(s.GenreId)).OrderBy(_ => EF.Functions.Random()).Take(affineWanted).ToListAsync(ct);
        var explore = await unrated.Where(s => !affine.Contains(s.GenreId)).OrderBy(_ => EF.Functions.Random()).Take(limit - picked.Count).ToListAsync(ct);
        picked.AddRange(explore);

        // Si la exploración no alcanzó, se completa con más afines en vez de devolver de menos.
        if (picked.Count < limit)
        {
            var ids = picked.Select(s => s.Id).ToList();
            picked.AddRange(await unrated.Where(s => affine.Contains(s.GenreId) && !ids.Contains(s.Id))
                .OrderBy(_ => EF.Functions.Random()).Take(limit - picked.Count).ToListAsync(ct));
        }

        Random.Shared.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(picked));
        return picked.Select(s => new SongDto(s.Id, s.Title, s.Artist, s.Genre!.Slug, s.Genre.Name, s.CoverUrl, s.PreviewUrl)).ToList();
    }

    public async Task<SwipeResponse> SwipeAsync(Guid userId, SwipeRequest request, CancellationToken ct)
    {
        var song = await db.Songs.AsNoTracking().Where(s => s.Id == request.SongId && s.IsActive)
            .Select(s => new { s.GenreId }).FirstOrDefaultAsync(ct)
            ?? throw ApiException.NotFound(ErrorCodes.SongNotFound, "La canción no existe.");

        var like = request.Direction == SwipeDirection.Like ? 1 : 0;
        var dislike = 1 - like;
        var now = clock.GetUtcNow();
        var direction = request.Direction.ToString();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // ON CONFLICT DO NOTHING: bajo concurrencia solo una petición inserta y por tanto solo ella suma al score.
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO swipes (user_id, song_id, direction, created_at)
            VALUES ({userId}, {request.SongId}, {direction}, {now})
            ON CONFLICT (user_id, song_id) DO NOTHING
            """, ct);

        if (inserted > 0)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO user_genre_scores (user_id, genre_id, likes, dislikes, imported_weight)
                VALUES ({userId}, {song.GenreId}, {like}, {dislike}, 0)
                ON CONFLICT (user_id, genre_id) DO UPDATE
                SET likes = user_genre_scores.likes + EXCLUDED.likes,
                    dislikes = user_genre_scores.dislikes + EXCLUDED.dislikes
                """, ct);
        }
        await tx.CommitAsync(ct);

        var count = await db.Swipes.CountAsync(s => s.UserId == userId, ct);
        return new SwipeResponse(inserted > 0, count);
    }

    public async Task<DnaResponse> GetDnaAsync(Guid userId, CancellationToken ct)
    {
        var weights = Dna.Normalize(await LoadVectorAsync(userId, ct));
        var genres = await db.Genres.AsNoTracking().ToDictionaryAsync(g => g.Id, ct);
        var list = weights
            .Where(kv => genres.ContainsKey(kv.Key))
            .Select(kv => new DnaGenreDto(genres[kv.Key].Slug, genres[kv.Key].Name, Math.Round(kv.Value, 4)))
            .OrderByDescending(g => g.Weight).ThenBy(g => g.Name)
            .ToList();

        var swipes = await db.Swipes.CountAsync(s => s.UserId == userId, ct);
        var threshold = matching.Value.RadarMinSwipes;
        return new DnaResponse(list, swipes, threshold, swipes >= threshold);
    }

    public async Task<DnaResponse> ImportSpotifyAsync(Guid userId, SpotifyImportRequest request, CancellationToken ct)
    {
        var slugWeights = new Dictionary<string, double>();
        for (var rank = 0; rank < request.TopArtists.Count; rank++)
        {
            var weight = 1 / (1 + RankDecay * rank);
            foreach (var slug in SpotifyGenreMap.Map(request.TopArtists[rank].Genres))
                slugWeights[slug] = slugWeights.GetValueOrDefault(slug) + weight;
        }

        var total = slugWeights.Values.Sum();
        var scale = total > MaxImportedTotal ? MaxImportedTotal / total : 1;
        var genreIds = await db.Genres.AsNoTracking().ToDictionaryAsync(g => g.Slug, g => g.Id, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serializa las importaciones del mismo usuario: dos en paralelo no se veían entre sí y sus aportes se sumaban pasando el tope.
        await AdvisoryLock.AcquireAsync(db, "dna-import", userId, ct);
        // La importación nueva reemplaza a la anterior: primero se limpia el aporte y luego se escribe el nuevo.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE user_genre_scores SET imported_weight = 0 WHERE user_id = {userId}", ct);
        foreach (var (slug, raw) in slugWeights)
        {
            var genreId = genreIds[slug];
            var imported = raw * scale;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO user_genre_scores (user_id, genre_id, likes, dislikes, imported_weight)
                VALUES ({userId}, {genreId}, 0, 0, {imported})
                ON CONFLICT (user_id, genre_id) DO UPDATE SET imported_weight = EXCLUDED.imported_weight
                """, ct);
        }
        await ReplaceArtistsAsync(userId, request.TopArtists, ct);
        await tx.CommitAsync(ct);

        return await GetDnaAsync(userId, ct);
    }

    private async Task ReplaceArtistsAsync(Guid userId, IReadOnlyList<SpotifyArtist> artists, CancellationToken ct)
    {
        await db.UserArtists.Where(a => a.UserId == userId).ExecuteDeleteAsync(ct);
        var now = clock.GetUtcNow();
        var seen = new HashSet<string>();
        for (var rank = 0; rank < artists.Count; rank++)
        {
            var artist = artists[rank];
            var key = ArtistKey.Of(artist.Name);
            // Un nombre sin letras ni dígitos no se puede cruzar ni reaccionar; un repetido conserva el mejor puesto.
            if (key.Length == 0 || !seen.Add(key)) continue;
            db.UserArtists.Add(new UserArtist
            {
                UserId = userId,
                Key = key,
                Name = artist.Name.Trim(),
                Rank = rank,
                ImageUrl = ArtistKey.SafeImageUrl(artist.ImageUrl),
                Genres = artist.Genres.ToList(),
                ImportedAt = now,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<int, double>> LoadVectorAsync(Guid userId, CancellationToken ct)
    {
        var scores = await db.UserGenreScores.AsNoTracking().Where(s => s.UserId == userId).ToListAsync(ct);
        var declared = await db.UserGenres.AsNoTracking().Where(g => g.UserId == userId).Select(g => g.GenreId).ToListAsync(ct);
        return Dna.Vector(scores, declared);
    }
}
