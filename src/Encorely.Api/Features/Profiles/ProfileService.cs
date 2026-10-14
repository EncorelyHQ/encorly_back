using Encorely.Api.Features.Events;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Profiles;

/// <param name="ReactionsReceived">Agregadas por objetivo y tipo, sin identidades.</param>
public sealed record OwnProfile(IReadOnlyList<ProfileArtistDto> Artists, ProfileStatsDto Stats, IReadOnlyList<ReceivedReactionDto> ReactionsReceived);

public sealed class ProfileService(AppDbContext db, IOptions<MatchingOptions> matching, TimeProvider clock)
{
    private const int MaxGenres = 8;
    private const int MaxArtists = 12;
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    // Fuente de los artistas del perfil (pendiente de confirmar con Camilo). false: solo el top importado de Spotify.
    // true: además, los artistas de las canciones con "Me gusta", para que el perfil no quede vacío sin Spotify.
    private static readonly bool IncludeLikedSongArtists = false;

    private sealed record Artist(string Key, string Name, string? ImageUrl);

    private sealed record Snapshot(Dictionary<int, double> Vector, IReadOnlyList<ProfileGenreDto> Genres, IReadOnlyList<Artist> Artists)
    {
        public bool Contains(ReactionTargetKind kind, string key) => kind == ReactionTargetKind.Genre
            ? Genres.Any(g => g.Slug == key)
            : Artists.Any(a => a.Key == key);

        public string? NameOf(ReactionTargetKind kind, string key) => kind == ReactionTargetKind.Genre
            ? Genres.FirstOrDefault(g => g.Slug == key)?.Name
            : Artists.FirstOrDefault(a => a.Key == key)?.Name;
    }

    /// <summary>
    /// Quién puede ver el perfil de otra persona (pendiente de confirmar con Camilo). Propuesta vigente: un match activo
    /// o un evento próximo en común (el mismo corte del radar y del like). Suspensión o bloqueo en cualquier sentido: no.
    /// </summary>
    private async Task<bool> CanViewAsync(Guid viewerId, Guid targetId, CancellationToken ct)
    {
        if (viewerId == targetId) return true;
        if (!await db.Users.AnyAsync(u => u.Id == targetId && u.Status == UserStatus.Active, ct)) return false;
        if (await db.Blocks.AnyAsync(b => (b.BlockerId == viewerId && b.BlockedId == targetId) || (b.BlockerId == targetId && b.BlockedId == viewerId), ct))
            return false;

        if (await db.Matches.AnyAsync(m => m.Status == MatchStatus.Active
                && ((m.UserAId == viewerId && m.UserBId == targetId) || (m.UserAId == targetId && m.UserBId == viewerId)), ct))
            return true;

        var cutoff = EventWindow.Cutoff(clock.GetUtcNow());
        return await (
            from mine in db.EventAttendances
            join theirs in db.EventAttendances on mine.EventId equals theirs.EventId
            join e in db.Events on mine.EventId equals e.Id
            where mine.UserId == viewerId && theirs.UserId == targetId && e.IsPublished && e.StartsAt > cutoff
            select e.Id).AnyAsync(ct);
    }

    public async Task<PublicProfileDto> GetAsync(Guid viewerId, Guid targetId, CancellationToken ct)
    {
        // 404 y nunca 403: no se revela si la cuenta existe, está suspendida o bloqueó a quien mira.
        if (!await CanViewAsync(viewerId, targetId, ct)) throw UserNotFound();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == targetId, ct);

        var genres = await db.Genres.AsNoTracking().ToDictionaryAsync(g => g.Id, ct);
        var theirs = await SnapshotAsync(targetId, genres, ct);
        var self = viewerId == targetId;
        var mine = self ? theirs : await SnapshotAsync(viewerId, genres, ct);

        var myArtistKeys = mine.Artists.Select(a => a.Key).ToHashSet();
        var artists = theirs.Artists.Select(a => new ProfileArtistDto(a.Key, a.Name, a.ImageUrl, !self && myArtistKeys.Contains(a.Key))).ToList();

        ProfileCommonDto common;
        IReadOnlyList<MyReactionDto> myReactions = [];
        if (self)
            common = new ProfileCommonDto(null, [], []);
        else
        {
            var withSignal = await WithSignalAsync([viewerId, targetId], ct);
            common = new ProfileCommonDto(
                withSignal.Count == 2 ? Dna.Affinity(mine.Vector, theirs.Vector) : null,
                Dna.Shared(mine.Vector, theirs.Vector).Select(id => genres[id].Slug).ToList(),
                artists.Where(a => a.Shared).Select(a => a.Key).ToList());
            myReactions = (await db.ProfileReactions.AsNoTracking().Where(r => r.FromUserId == viewerId && r.ToUserId == targetId).ToListAsync(ct))
                .Where(r => theirs.Contains(r.TargetKind, r.TargetKey))
                .OrderBy(r => r.TargetKind).ThenBy(r => r.TargetKey)
                .Select(r => new MyReactionDto(r.TargetKind, r.TargetKey, r.Reaction))
                .ToList();
        }

        return new PublicProfileDto(
            user.Id, user.DisplayName,
            user.BirthDate is { } birth ? User.AgeOn(birth, Today()) : null,
            user.City, user.Bio,
            theirs.Genres, artists, await StatsAsync(targetId, ct), common, myReactions);
    }

    /// <summary>Lo que <c>/me</c> agrega del perfil social: artistas, estadísticas y reacciones recibidas.</summary>
    public async Task<OwnProfile> GetOwnAsync(Guid userId, CancellationToken ct)
    {
        var genres = await db.Genres.AsNoTracking().ToDictionaryAsync(g => g.Id, ct);
        var snapshot = await SnapshotAsync(userId, genres, ct);

        // Solo de cuentas activas y sin bloqueo de por medio; los objetivos que ya no están en el perfil no se cuentan.
        var received = await (
            from r in db.ProfileReactions
            join u in db.Users on r.FromUserId equals u.Id
            where r.ToUserId == userId && u.Status == UserStatus.Active
                && !db.Blocks.Any(b => (b.BlockerId == userId && b.BlockedId == r.FromUserId) || (b.BlockerId == r.FromUserId && b.BlockedId == userId))
            group r by new { r.TargetKind, r.TargetKey, r.Reaction } into g
            select new { g.Key.TargetKind, g.Key.TargetKey, g.Key.Reaction, Count = g.Count() }).ToListAsync(ct);

        return new OwnProfile(
            snapshot.Artists.Select(a => new ProfileArtistDto(a.Key, a.Name, a.ImageUrl, false)).ToList(),
            await StatsAsync(userId, ct),
            received
                .Where(r => snapshot.Contains(r.TargetKind, r.TargetKey))
                .Select(r => new ReceivedReactionDto(r.TargetKind, r.TargetKey, snapshot.NameOf(r.TargetKind, r.TargetKey)!, r.Reaction, r.Count))
                .OrderByDescending(r => r.Count).ThenBy(r => r.TargetKind).ThenBy(r => r.TargetKey).ThenBy(r => r.Reaction)
                .ToList());
    }

    public async Task SetReactionAsync(Guid userId, Guid targetId, ReactionRequest request, CancellationToken ct)
    {
        if (userId == targetId)
            throw ApiException.BadRequest(ErrorCodes.CannotReactSelf, "No puedes reaccionar a tu propio perfil.");
        if (!await CanViewAsync(userId, targetId, ct)) throw UserNotFound();

        var key = request.TargetKey.Trim().ToLowerInvariant();
        var genres = await db.Genres.AsNoTracking().ToDictionaryAsync(g => g.Id, ct);
        if (!(await SnapshotAsync(targetId, genres, ct)).Contains(request.TargetKind, key))
            throw ApiException.BadRequest(ErrorCodes.ReactionTargetNotFound, "Eso ya no está en su perfil.");

        var now = clock.GetUtcNow();
        var kind = request.TargetKind.ToString();
        var reaction = request.Reaction.ToString();
        // Upsert: dos PUT en paralelo al mismo objetivo no chocan con la PK; gana el último tipo.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO profile_reactions (from_user_id, to_user_id, target_kind, target_key, reaction, created_at, updated_at)
            VALUES ({userId}, {targetId}, {kind}, {key}, {reaction}, {now}, {now})
            ON CONFLICT (from_user_id, to_user_id, target_kind, target_key)
            DO UPDATE SET reaction = EXCLUDED.reaction, updated_at = EXCLUDED.updated_at
            """, ct);
    }

    /// <summary>Idempotente y siempre 204: no revela si había reacción ni si el perfil se puede ver.</summary>
    public async Task RemoveReactionAsync(Guid userId, Guid targetId, ReactionTargetKind kind, string targetKey, CancellationToken ct)
    {
        var key = targetKey.Trim().ToLowerInvariant();
        await db.ProfileReactions
            .Where(r => r.FromUserId == userId && r.ToUserId == targetId && r.TargetKind == kind && r.TargetKey == key)
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Personas con señal propia para la afinidad: <c>AffinityMinSwipes</c> swipes o Spotify importado. El radar
    /// replica esta regla en SQL (<c>RadarService</c>).
    /// </summary>
    private async Task<HashSet<Guid>> WithSignalAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var min = matching.Value.AffinityMinSwipes;
        if (min <= 0) return ids.ToHashSet();

        var result = (await db.Swipes.Where(s => ids.Contains(s.UserId)).GroupBy(s => s.UserId)
            .Where(g => g.Count() >= min).Select(g => g.Key).ToListAsync(ct)).ToHashSet();
        result.UnionWith(await db.UserArtists.Where(a => ids.Contains(a.UserId)).Select(a => a.UserId).Distinct().ToListAsync(ct));
        result.UnionWith(await db.UserGenreScores.Where(s => ids.Contains(s.UserId) && s.ImportedWeight > 0).Select(s => s.UserId).Distinct().ToListAsync(ct));
        return result;
    }

    private async Task<Snapshot> SnapshotAsync(Guid userId, IReadOnlyDictionary<int, Genre> genres, CancellationToken ct)
    {
        var scores = await db.UserGenreScores.AsNoTracking().Where(s => s.UserId == userId).ToListAsync(ct);
        var declared = await db.UserGenres.AsNoTracking().Where(g => g.UserId == userId).Select(g => g.GenreId).ToListAsync(ct);
        var vector = Dna.Vector(scores, declared);

        var topGenres = vector.Where(kv => genres.ContainsKey(kv.Key))
            .OrderByDescending(kv => kv.Value).ThenBy(kv => genres[kv.Key].Name)
            .Take(MaxGenres)
            .Select(kv => new ProfileGenreDto(genres[kv.Key].Slug, genres[kv.Key].Name))
            .ToList();

        var artists = await db.UserArtists.AsNoTracking().Where(a => a.UserId == userId).OrderBy(a => a.Rank).Take(MaxArtists)
            .Select(a => new Artist(a.Key, a.Name, a.ImageUrl)).ToListAsync(ct);
        if (IncludeLikedSongArtists && artists.Count < MaxArtists)
        {
            var liked = await db.Swipes.Where(s => s.UserId == userId && s.Direction == SwipeDirection.Like)
                .Join(db.Songs, s => s.SongId, song => song.Id, (_, song) => song.Artist)
                .GroupBy(name => name).Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).ThenBy(x => x.Name).Take(MaxArtists * 2).ToListAsync(ct);
            var keys = artists.Select(a => a.Key).ToHashSet();
            foreach (var name in liked.Select(x => x.Name))
            {
                var key = ArtistKey.Of(name);
                if (key.Length > 0 && keys.Add(key)) artists.Add(new Artist(key, name, null));
                if (artists.Count == MaxArtists) break;
            }
        }

        return new Snapshot(vector, topGenres, artists);
    }

    private async Task<ProfileStatsDto> StatsAsync(Guid userId, CancellationToken ct)
    {
        var swipes = await db.Swipes.CountAsync(s => s.UserId == userId, ct);
        var explored = await db.Swipes.Where(s => s.UserId == userId)
            .Join(db.Songs, s => s.SongId, song => song.Id, (_, song) => song.GenreId).Distinct().CountAsync(ct);
        var events = await db.EventAttendances.CountAsync(a => a.UserId == userId, ct);
        return new ProfileStatsDto(swipes, explored, events);
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(ColombiaOffset).DateTime);

    private static ApiException UserNotFound() => ApiException.NotFound(ErrorCodes.UserNotFound, "No encontramos a esa persona.");
}
