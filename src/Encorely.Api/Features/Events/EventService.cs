using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Api.Features.Events;

public sealed class EventService(AppDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<EventDto>> ListAsync(Guid userId, string? city, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100)
            throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "El límite debe estar entre 1 y 100.");

        var now = clock.GetUtcNow();
        var query = Published().Where(e => e.StartsAt > now);

        if (Text.Clean(city) is { } requested)
        {
            // Conjunto pequeño de ciudades: se resuelve la canónica en memoria para tolerar acentos y mayúsculas.
            var wanted = Fold(requested);
            var cities = await Published().Select(e => e.City).Distinct().ToListAsync(ct);
            var canonical = cities.Where(c => Fold(c) == wanted).ToList();
            if (canonical.Count == 0) return [];
            query = query.Where(e => canonical.Contains(e.City));
        }

        return await ProjectDto(query.OrderBy(e => e.StartsAt).ThenBy(e => e.Name).Take(limit), userId).ToListAsync(ct);
    }

    public async Task<EventDto> GetAsync(Guid userId, Guid eventId, CancellationToken ct)
        => await FindAsync(userId, eventId, ct) ?? throw EventNotFound();

    public async Task<EventDto> AttendAsync(Guid userId, Guid eventId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var startsAt = await Published().Where(e => e.Id == eventId).Select(e => (DateTimeOffset?)e.StartsAt).FirstOrDefaultAsync(ct)
            ?? throw EventNotFound();
        if (startsAt <= now) throw ApiException.BadRequest(ErrorCodes.EventPast, "El evento ya pasó.");

        // Atómico: dos peticiones simultáneas no chocan con la PK.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO event_attendances (event_id, user_id, created_at) VALUES ({eventId}, {userId}, {now}) ON CONFLICT DO NOTHING", ct);

        return await FindAsync(userId, eventId, ct) ?? throw EventNotFound();
    }

    public async Task<EventDto> UnattendAsync(Guid userId, Guid eventId, CancellationToken ct)
    {
        if (!await Published().AnyAsync(e => e.Id == eventId, ct)) throw EventNotFound();
        await db.EventAttendances.Where(a => a.EventId == eventId && a.UserId == userId).ExecuteDeleteAsync(ct);
        return await FindAsync(userId, eventId, ct) ?? throw EventNotFound();
    }

    public async Task<IReadOnlyList<EventDto>> MyEventsAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var query = Published().Where(e => e.StartsAt > now
            && db.EventAttendances.Any(a => a.EventId == e.Id && a.UserId == userId));
        return await ProjectDto(query.OrderBy(e => e.StartsAt).ThenBy(e => e.Name), userId).ToListAsync(ct);
    }

    private IQueryable<Event> Published() => db.Events.AsNoTracking().Where(e => e.IsPublished);

    private Task<EventDto?> FindAsync(Guid userId, Guid eventId, CancellationToken ct)
        => ProjectDto(Published().Where(e => e.Id == eventId), userId).FirstOrDefaultAsync(ct);

    // Conteo e "yo voy" como subconsultas de la misma proyección: sin N+1.
    private IQueryable<EventDto> ProjectDto(IQueryable<Event> events, Guid userId) => events.Select(e => new EventDto(
        e.Id, e.Name, e.ArtistLineup, e.Venue, e.City, e.StartsAt, e.ImageUrl, e.Description,
        db.EventAttendances.Count(a => a.EventId == e.Id),
        db.EventAttendances.Any(a => a.EventId == e.Id && a.UserId == userId)));

    private static ApiException EventNotFound() => ApiException.NotFound(ErrorCodes.EventNotFound, "No encontramos ese evento.");

    private const string Accented = "áàäâãéèëêíìïîóòöôõúùüûñç";
    private const string Plain = "aaaaaeeeeiiiiooooouuuunc";

    // Mapa explícito: el proyecto usa InvariantGlobalization y string.Normalize no descompone ahí.
    private static string Fold(string value)
    {
        var chars = value.Trim().ToLowerInvariant().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var at = Accented.IndexOf(chars[i]);
            if (at >= 0) chars[i] = Plain[at];
        }
        return new string(chars);
    }
}
