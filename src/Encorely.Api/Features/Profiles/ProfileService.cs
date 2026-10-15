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

    // Fuente de los artistas del perfil. true (decisión de Camilo): el top de Spotify primero y luego los artistas de las
    // canciones con "Me gusta", para que el perfil no quede vacío sin Spotify. false: solo Spotify.
    private static readonly bool IncludeLikedSongArtists = true;
    private const int MaxReceivedPage = 50;

    /// <summary>
    /// Versión del consentimiento que agregó la finalidad "perfil público" (Ley 1581: consentimiento previo, expreso e
    /// informado). Quien no ha aceptado esa versión o una posterior no se muestra a otros ni recibe reacciones.
    /// Las versiones son fechas ISO: se comparan como texto.
    /// </summary>
    public const string PublicProfileConsentSince = "2026-10-14";

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
    /// Quién puede ver el perfil de otra persona: cualquier usuario autenticado (decisión de Camilo, 2026-10-14), salvo
    /// bloqueo en cualquier sentido o cuenta suspendida. La contención es bloquear y reportar.
    /// </summary>
    private async Task<bool> CanViewAsync(Guid viewerId, Guid targetId, CancellationToken ct)
    {
        if (viewerId == targetId) return true;
        // Perfil apagado o sin el consentimiento de la finalidad: el mismo 404 que con bloqueo, sin revelar que existe.
        if (!await VisibleUsers().AnyAsync(u => u.Id == targetId, ct)) return false;
        return !await db.Blocks.AnyAsync(b => (b.BlockerId == viewerId && b.BlockedId == targetId) || (b.BlockerId == targetId && b.BlockedId == viewerId), ct);
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

        var received = await ReceivedQuery(userId)
            .GroupBy(r => new { r.TargetKind, r.TargetKey, r.Reaction })
            .Select(g => new { g.Key.TargetKind, g.Key.TargetKey, g.Key.Reaction, TargetName = g.Max(r => r.TargetName), Count = g.Count() })
            .ToListAsync(ct);

        return new OwnProfile(
            snapshot.Artists.Select(a => new ProfileArtistDto(a.Key, a.Name, a.ImageUrl, false)).ToList(),
            await StatsAsync(userId, ct),
            received
                .Select(r => new ReceivedReactionDto(r.TargetKind, r.TargetKey, r.TargetName!, r.Reaction, r.Count))
                .OrderByDescending(r => r.Count).ThenBy(r => r.TargetKind).ThenBy(r => r.TargetKey).ThenBy(r => r.Reaction)
                .ToList());
    }

    /// <summary>
    /// Quién reaccionó a mi perfil, de la más reciente a la más vieja (decisión de Camilo: se ve quién). Paginado por
    /// clave con un cursor opaco: las reacciones nuevas no desplazan ni repiten las páginas ya leídas.
    /// </summary>
    public async Task<ReceivedReactionPage> ListReceivedAsync(Guid userId, string? before, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > MaxReceivedPage)
            throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "El límite debe estar entre 1 y 50.");

        var query = ReceivedQuery(userId);
        if (before is not null)
        {
            var c = ReactionCursor.Parse(before)
                ?? throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "Revisa los datos enviados.", "before: cursor inválido.");
            // Orden total (fecha, quién, tipo de objetivo, objetivo), descendente.
            query = query.Where(r => r.UpdatedAt < c.At
                || (r.UpdatedAt == c.At && (r.FromUserId.CompareTo(c.From) < 0
                    || (r.FromUserId == c.From && (r.TargetKind < c.Kind
                        || (r.TargetKind == c.Kind && string.Compare(r.TargetKey, c.Key) < 0))))));
        }

        var rows = await query
            .OrderByDescending(r => r.UpdatedAt).ThenByDescending(r => r.FromUserId).ThenByDescending(r => r.TargetKind).ThenByDescending(r => r.TargetKey)
            .Take(limit + 1)
            .Join(db.Users, r => r.FromUserId, u => u.Id, (r, u) => new { r, u.DisplayName })
            .ToListAsync(ct);
        // El Join tras el Take no garantiza el orden: se reordena en memoria con la misma clave.
        rows = rows.OrderByDescending(x => x.r.UpdatedAt).ThenByDescending(x => x.r.FromUserId).ThenByDescending(x => x.r.TargetKind)
            .ThenByDescending(x => x.r.TargetKey, StringComparer.Ordinal).ToList();

        var page = rows.Take(limit).ToList();
        var last = page.LastOrDefault()?.r;
        return new ReceivedReactionPage(
            page.Select(x => new ReceivedReactionItemDto(
                new ReactionFromDto(x.r.FromUserId, x.DisplayName), x.r.TargetKind, x.r.TargetKey, x.r.TargetName, x.r.Reaction, x.r.UpdatedAt)).ToList(),
            rows.Count > limit && last is not null ? ReactionCursor.Format(last) : null);
    }

    /// <summary>
    /// Reacciones recibidas que se muestran: el dueño con el perfil público activo, y de quien reaccionó, también con su
    /// perfil público activo (la reacción muestra su nombre) y sin bloqueo en ningún sentido. Las demás se conservan.
    /// </summary>
    private IQueryable<ProfileReaction> ReceivedQuery(Guid userId)
        => from r in db.ProfileReactions.AsNoTracking()
           join u in VisibleUsers() on r.FromUserId equals u.Id
           where r.ToUserId == userId && VisibleUsers().Any(owner => owner.Id == userId)
               && !db.Blocks.Any(b => (b.BlockerId == userId && b.BlockedId == r.FromUserId) || (b.BlockerId == r.FromUserId && b.BlockedId == userId))
           select r;

    /// <summary>Cuentas cuyo perfil público se muestra: activas, con el interruptor encendido y la finalidad consentida.</summary>
    private IQueryable<User> VisibleUsers()
        => db.Users.Where(u => u.Status == UserStatus.Active && u.ShowProfileToOthers
            && db.UserConsents.Any(c => c.UserId == u.Id && string.Compare(c.Version, PublicProfileConsentSince) >= 0));

    /// <summary>true si el perfil del usuario se muestra a otros (para <c>/me</c>).</summary>
    public Task<bool> IsVisibleAsync(Guid userId, CancellationToken ct) => VisibleUsers().AnyAsync(u => u.Id == userId, ct);

    public async Task SetReactionAsync(Guid userId, Guid targetId, ReactionRequest request, CancellationToken ct)
    {
        if (userId == targetId)
            throw ApiException.BadRequest(ErrorCodes.CannotReactSelf, "No puedes reaccionar a tu propio perfil.");
        if (!await CanViewAsync(userId, targetId, ct)) throw UserNotFound();

        var key = request.TargetKey.Trim().ToLowerInvariant();
        var genres = await db.Genres.AsNoTracking().ToDictionaryAsync(g => g.Id, ct);
        // El nombre se guarda con la reacción: quien la recibe la ve aunque ese objetivo ya no esté en su perfil.
        var name = (await SnapshotAsync(targetId, genres, ct)).NameOf(request.TargetKind, key)
            ?? throw ApiException.BadRequest(ErrorCodes.ReactionTargetNotFound, "Eso ya no está en su perfil.");

        var now = clock.GetUtcNow();
        var kind = request.TargetKind.ToString();
        var reaction = request.Reaction.ToString();
        // Upsert: dos PUT en paralelo al mismo objetivo no chocan con la PK; gana el último tipo.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO profile_reactions (from_user_id, to_user_id, target_kind, target_key, target_name, reaction, created_at, updated_at)
            VALUES ({userId}, {targetId}, {kind}, {key}, {name}, {reaction}, {now}, {now})
            ON CONFLICT (from_user_id, to_user_id, target_kind, target_key)
            DO UPDATE SET reaction = EXCLUDED.reaction, target_name = EXCLUDED.target_name, updated_at = EXCLUDED.updated_at
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
