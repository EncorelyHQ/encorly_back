using System.ComponentModel;
using System.Security.Claims;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Encorely.Api.Features.Events;

public static class EventEndpoints
{
    public static void MapEvents(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("").WithTags("Eventos");

        g.MapGet("/events", List).WithName("ListEvents").WithSummary("Eventos futuros publicados, por fecha").Problems(400, 401);
        g.MapGet("/events/{id:guid}", Get).WithName("GetEvent").WithSummary("Detalle de un evento").Problems(401, 404);
        g.MapPut("/events/{id:guid}/attendance", Attend).WithName("AttendEvent").WithSummary("Marco que voy al evento (idempotente)").Problems(400, 401, 404);
        g.MapDelete("/events/{id:guid}/attendance", Unattend).WithName("UnattendEvent").WithSummary("Ya no voy al evento (idempotente)").Problems(401, 404);
        g.MapGet("/me/events", MyEvents).WithName("GetMyEvents").WithSummary("Eventos futuros a los que voy").Problems(401);
        g.MapGet("/events/{id:guid}/radar", Radar).WithName("GetEventRadar")
            .WithSummary("Asistentes del evento ordenados por afinidad real. Requiere ir al evento y haber desbloqueado el radar")
            .Problems(401, 403, 404);
    }

    private static async Task<Ok<IReadOnlyList<EventDto>>> List(
        ClaimsPrincipal user, EventService events, CancellationToken ct,
        [Description("Filtra por ciudad (p. ej. Bogotá, Medellín)")] string? city = null,
        [Description("1–100, por defecto 50")] int limit = 50)
        => TypedResults.Ok(await events.ListAsync(user.UserId(), city, limit, ct));

    private static async Task<Ok<EventDto>> Get(Guid id, ClaimsPrincipal user, EventService events, CancellationToken ct)
        => TypedResults.Ok(await events.GetAsync(user.UserId(), id, ct));

    private static async Task<Ok<EventDto>> Attend(Guid id, ClaimsPrincipal user, EventService events, CancellationToken ct)
        => TypedResults.Ok(await events.AttendAsync(user.UserId(), id, ct));

    private static async Task<Ok<EventDto>> Unattend(Guid id, ClaimsPrincipal user, EventService events, CancellationToken ct)
        => TypedResults.Ok(await events.UnattendAsync(user.UserId(), id, ct));

    private static async Task<Ok<IReadOnlyList<EventDto>>> MyEvents(ClaimsPrincipal user, EventService events, CancellationToken ct)
        => TypedResults.Ok(await events.MyEventsAsync(user.UserId(), ct));

    private static async Task<Ok<IReadOnlyList<RadarEntryDto>>> Radar(Guid id, ClaimsPrincipal user, RadarService radar, CancellationToken ct)
        => TypedResults.Ok(await radar.GetAsync(user.UserId(), id, ct));
}
