using System.ComponentModel;
using System.Security.Claims;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Encorely.Api.Features.Admin;

public static class AdminEndpoints
{
    public static void MapAdmin(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/admin").WithTags("Administración")
            .RequireAuthorization(AuthSetup.AdminPolicy)
            .AddEndpointFilter<AdminRoleFilter>();

        g.MapGet("/events", ListEvents).WithName("AdminListEvents").WithSummary("Eventos (incluye no publicados y, opcionalmente, pasados)").Problems(401, 403);
        g.MapPost("/events", CreateEvent).WithName("AdminCreateEvent").WithSummary("Crea un evento").Problems(400, 401, 403);
        g.MapPut("/events/{id:guid}", UpdateEvent).WithName("AdminUpdateEvent").WithSummary("Edita un evento").Problems(400, 401, 403, 404);
        g.MapDelete("/events/{id:guid}", DeleteEvent).WithName("AdminDeleteEvent").WithSummary("Borra un evento y sus asistencias").Problems(401, 403, 404);

        g.MapGet("/songs", ListSongs).WithName("AdminListSongs").WithSummary("Catálogo de canciones con conteo de likes/dislikes").Problems(400, 401, 403);
        g.MapPost("/songs", CreateSong).WithName("AdminCreateSong").WithSummary("Agrega una canción al catálogo").Problems(400, 401, 403, 409);
        g.MapPut("/songs/{id:guid}", UpdateSong).WithName("AdminUpdateSong").WithSummary("Edita una canción (isActive=false la saca del feed)").Problems(400, 401, 403, 404, 409);
        g.MapGet("/itunes/search", SearchITunes).WithName("AdminSearchITunes").WithSummary("Busca en iTunes Search para dar de alta canciones con preview").Problems(400, 401, 403, 502);

        g.MapGet("/reports", ListReports).WithName("AdminListReports").WithSummary("Reportes, los más antiguos primero").Problems(401, 403);
        g.MapPost("/reports/{id:guid}/resolve", ResolveReport).WithName("AdminResolveReport")
            .WithSummary("Resuelve un reporte; ban suspende al reportado, cierra sus matches y revoca sus sesiones").Problems(400, 401, 403, 404);
        g.MapPut("/users/{id:guid}/status", SetUserStatus).WithName("AdminSetUserStatus").WithSummary("Suspende o reactiva una cuenta").Problems(400, 401, 403, 404);
    }

    private static async Task<Ok<IReadOnlyList<AdminEventDto>>> ListEvents(AdminService admin, CancellationToken ct,
        [Description("Incluye eventos pasados")] bool includePast = false)
        => TypedResults.Ok(await admin.ListEventsAsync(includePast, ct));

    private static async Task<Created<AdminEventDto>> CreateEvent(AdminEventRequest request, AdminService admin, CancellationToken ct)
    {
        var dto = await admin.CreateEventAsync(request, ct);
        return TypedResults.Created($"/api/v1/admin/events/{dto.Id}", dto);
    }

    private static async Task<Ok<AdminEventDto>> UpdateEvent(Guid id, AdminEventRequest request, AdminService admin, CancellationToken ct)
        => TypedResults.Ok(await admin.UpdateEventAsync(id, request, ct));

    private static async Task<NoContent> DeleteEvent(Guid id, AdminService admin, CancellationToken ct)
    {
        await admin.DeleteEventAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<AdminSongDto>>> ListSongs(AdminService admin, CancellationToken ct,
        [Description("Busca en título o artista")] string? query = null,
        [Description("Slug de género")] string? genre = null,
        [Description("1–200, por defecto 100")] int limit = 100)
        => TypedResults.Ok(await admin.ListSongsAsync(query, genre, limit, ct));

    private static async Task<Created<AdminSongDto>> CreateSong(AdminSongRequest request, AdminService admin, CancellationToken ct)
    {
        var dto = await admin.CreateSongAsync(request, ct);
        return TypedResults.Created($"/api/v1/admin/songs/{dto.Id}", dto);
    }

    private static async Task<Ok<AdminSongDto>> UpdateSong(Guid id, AdminSongRequest request, AdminService admin, CancellationToken ct)
        => TypedResults.Ok(await admin.UpdateSongAsync(id, request, ct));

    private static async Task<Ok<IReadOnlyList<ITunesTrackDto>>> SearchITunes(ITunesClient itunes, CancellationToken ct,
        [Description("Artista y/o título")] string term = "")
        => TypedResults.Ok(await itunes.SearchAsync(term, ct));

    private static async Task<Ok<IReadOnlyList<AdminReportDto>>> ListReports(AdminService admin, CancellationToken ct,
        [Description("Filtra por estado; por defecto open")] ReportStatus status = ReportStatus.Open)
        => TypedResults.Ok(await admin.ListReportsAsync(status, ct));

    private static async Task<Ok<AdminReportDto>> ResolveReport(Guid id, ResolveReportRequest request, ClaimsPrincipal user, AdminService admin, CancellationToken ct)
        => TypedResults.Ok(await admin.ResolveReportAsync(user.UserId(), id, request, ct));

    private static async Task<NoContent> SetUserStatus(Guid id, UserStatusRequest request, ClaimsPrincipal user, AdminService admin, CancellationToken ct)
    {
        await admin.SetUserStatusAsync(user.UserId(), id, request.Status, ct);
        return TypedResults.NoContent();
    }
}
