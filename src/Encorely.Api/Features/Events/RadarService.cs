using System.Data;
using Dapper;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Events;

/// <summary>Radar por evento. Única consulta con Dapper: afinidad calculada en SQL sobre los asistentes.</summary>
public sealed class RadarService(AppDbContext db, IOptions<MatchingOptions> matching, TimeProvider clock)
{
    private const int MaxEntries = 200;
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    // El coseno replica Dna.RawWeight/Dna.Affinity; las constantes llegan como parámetros para no duplicarlas.
    // Los géneros declarados sin score entran por user_genres. Round sobre numeric = mitad hacia fuera, como en C#.
    private const string Sql = """
        WITH cand AS (
            SELECT ea.user_id
            FROM event_attendances ea
            JOIN users u ON u.id = ea.user_id
            WHERE ea.event_id = @eventId AND ea.user_id <> @me AND u.status = 'Active'
              AND NOT EXISTS (SELECT 1 FROM blocks b
                              WHERE (b.blocker_id = @me AND b.blocked_id = ea.user_id)
                                 OR (b.blocker_id = ea.user_id AND b.blocked_id = @me))
        ),
        ids AS (SELECT user_id FROM cand UNION ALL SELECT @me),
        -- Señal propia para la afinidad (replica ProfileService.WithSignalAsync): swipes suficientes o Spotify importado.
        sig AS (
            SELECT i.user_id FROM ids i
            WHERE (SELECT count(*) FROM swipes sw WHERE sw.user_id = i.user_id) >= @minSignalSwipes
               OR EXISTS (SELECT 1 FROM user_artists ua WHERE ua.user_id = i.user_id)
               OR EXISTS (SELECT 1 FROM user_genre_scores gs WHERE gs.user_id = i.user_id AND gs.imported_weight > 0)
        ),
        keys AS (
            SELECT s.user_id, s.genre_id FROM user_genre_scores s JOIN ids i ON i.user_id = s.user_id
            UNION
            SELECT g.user_id, g.genre_id FROM user_genres g JOIN ids i ON i.user_id = g.user_id
        ),
        w AS (
            SELECT k.user_id, k.genre_id,
                   GREATEST(0::float8,
                       COALESCE(s.likes, 0) * @likeWeight - COALESCE(s.dislikes, 0) * @dislikePenalty
                       + COALESCE(s.imported_weight, 0)
                       + CASE WHEN ug.user_id IS NULL THEN 0 ELSE @declaredBonus END) AS weight
            FROM keys k
            LEFT JOIN user_genre_scores s ON s.user_id = k.user_id AND s.genre_id = k.genre_id
            LEFT JOIN user_genres ug ON ug.user_id = k.user_id AND ug.genre_id = k.genre_id
        ),
        wp AS (SELECT user_id, genre_id, weight FROM w WHERE weight > 0),
        norms AS (SELECT user_id, sqrt(sum(weight * weight)) AS norm FROM wp GROUP BY user_id),
        dots AS (
            SELECT c.user_id, sum(c.weight * m.weight) AS dot
            FROM wp c JOIN wp m ON m.genre_id = c.genre_id AND m.user_id = @me
            WHERE c.user_id <> @me
            GROUP BY c.user_id
        ),
        scored AS (
            SELECT cand.user_id,
                   CASE WHEN NOT EXISTS (SELECT 1 FROM sig WHERE user_id = @me)
                          OR NOT EXISTS (SELECT 1 FROM sig WHERE user_id = cand.user_id) THEN NULL
                        WHEN nc.norm IS NULL OR (SELECT norm FROM norms WHERE user_id = @me) IS NULL THEN 0
                        ELSE round((100 * COALESCE(d.dot, 0) / (nc.norm * (SELECT norm FROM norms WHERE user_id = @me)))::numeric)::int
                   END AS affinity
            FROM cand
            LEFT JOIN norms nc ON nc.user_id = cand.user_id
            LEFT JOIN dots d ON d.user_id = cand.user_id
        )
        SELECT u.id AS userid, u.display_name AS displayname, u.birth_date AS birthdate, u.city AS city,
               sc.affinity AS affinity,
               (SELECT array_agg(x.genre_id ORDER BY x.genre_id) FROM wp x WHERE x.user_id = u.id) AS genreids,
               (SELECT array_agg(x.weight ORDER BY x.genre_id) FROM wp x WHERE x.user_id = u.id) AS weights,
               EXISTS (SELECT 1 FROM user_likes l WHERE l.from_user_id = @me AND l.to_user_id = u.id) AS likedbyme,
               EXISTS (SELECT 1 FROM matches m
                       WHERE m.status = 'Active'
                         AND ((m.user_a_id = @me AND m.user_b_id = u.id) OR (m.user_a_id = u.id AND m.user_b_id = @me))) AS matched
        FROM scored sc
        JOIN users u ON u.id = sc.user_id
        WHERE sc.affinity IS NULL OR sc.affinity >= @minAffinity
        ORDER BY sc.affinity DESC NULLS LAST, u.display_name, u.id
        LIMIT @max
        """;

    public async Task<IReadOnlyList<RadarEntryDto>> GetAsync(Guid userId, Guid eventId, CancellationToken ct)
    {
        var startsAt = await db.Events.Where(e => e.Id == eventId && e.IsPublished).Select(e => (DateTimeOffset?)e.StartsAt).FirstOrDefaultAsync(ct)
            ?? throw ApiException.NotFound(ErrorCodes.EventNotFound, "No encontramos ese evento.");
        // Mismo corte que el like: un radar donde nadie puede dar like es una vista muerta.
        if (startsAt <= EventWindow.Cutoff(clock.GetUtcNow()))
            throw ApiException.BadRequest(ErrorCodes.EventPast, "El evento ya pasó.");

        if (!await db.EventAttendances.AnyAsync(a => a.EventId == eventId && a.UserId == userId, ct))
            throw ApiException.Forbidden(ErrorCodes.NotAttending, "Marca que vas al evento para ver quién más va.");

        var swipes = await db.Swipes.CountAsync(s => s.UserId == userId, ct);
        var missing = matching.Value.RadarMinSwipes - swipes;
        if (missing > 0)
            throw ApiException.Forbidden(ErrorCodes.RadarLocked, "Aún no puedes ver el radar.", $"Te faltan {missing} canciones por calificar.");

        var genres = await db.Genres.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Slug, ct);
        var myScores = await db.UserGenreScores.AsNoTracking().Where(s => s.UserId == userId).ToListAsync(ct);
        var myDeclared = await db.UserGenres.AsNoTracking().Where(g => g.UserId == userId).Select(g => g.GenreId).ToListAsync(ct);
        var mine = Dna.Vector(myScores, myDeclared);

        var rows = await QueryAsync(userId, eventId, ct);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(ColombiaOffset).Date);

        return rows.Select(r =>
        {
            var theirs = new Dictionary<int, double>();
            if (r.GenreIds is not null && r.Weights is not null)
                for (var i = 0; i < r.GenreIds.Length; i++) theirs[r.GenreIds[i]] = r.Weights[i];

            return new RadarEntryDto(
                r.UserId,
                r.DisplayName,
                r.BirthDate is { } birth ? User.AgeOn(DateOnly.FromDateTime(birth), today) : null,
                r.City,
                r.Affinity,
                Dna.Shared(mine, theirs).Select(id => genres[id]).ToList(),
                r.LikedByMe,
                r.Matched);
        }).ToList();
    }

    private async Task<IEnumerable<Row>> QueryAsync(Guid userId, Guid eventId, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            return await connection.QueryAsync<Row>(new CommandDefinition(Sql, new
            {
                eventId,
                me = userId,
                likeWeight = Dna.LikeWeight,
                dislikePenalty = Dna.DislikePenalty,
                declaredBonus = Dna.DeclaredBonus,
                minAffinity = matching.Value.RadarMinAffinity,
                minSignalSwipes = matching.Value.AffinityMinSwipes,
                max = MaxEntries,
            }, cancellationToken: ct));
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    // Clase con setters: Dapper mapea por nombre (alias en minúsculas) y Npgsql entrega date como DateTime.
    private sealed class Row
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = "";
        public DateTime? BirthDate { get; set; }
        public string? City { get; set; }
        public int? Affinity { get; set; }
        public int[]? GenreIds { get; set; }
        public double[]? Weights { get; set; }
        public bool LikedByMe { get; set; }
        public bool Matched { get; set; }
    }
}
