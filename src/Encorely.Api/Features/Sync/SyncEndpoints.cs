using System.Security.Claims;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Encorely.Api.Features.Sync;

public static class SyncEndpoints
{
    public static void MapSync(this IEndpointRouteBuilder api)
    {
        var backup = api.MapGroup("/me/backup").WithTags("Respaldo cifrado");
        backup.MapPut("", PutBackup).WithName("PutBackup").WithSummary("Sube (reemplaza) mi respaldo cifrado en el dispositivo").Problems(400, 401, 413);
        backup.MapGet("", GetBackup).WithName("GetBackup").WithSummary("Descarga mi respaldo cifrado").Problems(401, 404);
        backup.MapGet("/info", GetBackupInfo).WithName("GetBackupInfo").WithSummary("Tamaño y fecha de mi respaldo, sin descargarlo").Problems(401, 404);
        backup.MapDelete("", DeleteBackup).WithName("DeleteBackup").WithSummary("Borra mi respaldo").Problems(401);

        var link = api.MapGroup("/keys/link-requests").WithTags("Vincular dispositivo");
        link.MapPost("", Create).WithName("CreateLinkRequest")
            .WithSummary("Dispositivo NUEVO: abre un buzón de vinculación (vence en 10 min)").Problems(400, 401, 429).UserWrites();
        link.MapGet("/{id:guid}", Get).WithName("GetLinkRequest").WithSummary("Consulta el buzón (el nuevo lo sondea hasta que esté ready)").Problems(401, 404);
        link.MapPut("/{id:guid}", Fulfill).WithName("FulfillLinkRequest")
            .WithSummary("Dispositivo VIEJO: deja el material de llaves cifrado hacia la llave efímera del nuevo (una sola vez)").Problems(400, 401, 404, 409);
        link.MapDelete("/{id:guid}", Consume).WithName("DeleteLinkRequest").WithSummary("El nuevo ya recogió el material: se destruye el buzón").Problems(401);
    }

    private static async Task<Ok<BackupInfoDto>> PutBackup(PutBackupRequest request, ClaimsPrincipal user, SyncService sync, CancellationToken ct)
        => TypedResults.Ok(await sync.PutBackupAsync(user.UserId(), request, ct));

    private static async Task<Ok<BackupDto>> GetBackup(ClaimsPrincipal user, SyncService sync, CancellationToken ct)
        => TypedResults.Ok(await sync.GetBackupAsync(user.UserId(), ct));

    private static async Task<Ok<BackupInfoDto>> GetBackupInfo(ClaimsPrincipal user, SyncService sync, CancellationToken ct)
        => TypedResults.Ok(await sync.GetBackupInfoAsync(user.UserId(), ct));

    private static async Task<NoContent> DeleteBackup(ClaimsPrincipal user, SyncService sync, CancellationToken ct)
    {
        await sync.DeleteBackupAsync(user.UserId(), ct);
        return TypedResults.NoContent();
    }

    private static async Task<Created<LinkRequestDto>> Create(CreateLinkRequest request, ClaimsPrincipal user, SyncService sync, CancellationToken ct)
    {
        var dto = await sync.CreateLinkAsync(user.UserId(), request, ct);
        return TypedResults.Created($"/api/v1/keys/link-requests/{dto.Id}", dto);
    }

    private static async Task<Ok<LinkRequestDto>> Get(Guid id, ClaimsPrincipal user, SyncService sync, CancellationToken ct)
        => TypedResults.Ok(await sync.GetLinkAsync(user.UserId(), id, ct));

    private static async Task<NoContent> Fulfill(Guid id, FulfillLinkRequest request, ClaimsPrincipal user, SyncService sync, CancellationToken ct)
    {
        await sync.FulfillLinkAsync(user.UserId(), id, request, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> Consume(Guid id, ClaimsPrincipal user, SyncService sync, CancellationToken ct)
    {
        await sync.DeleteLinkAsync(user.UserId(), id, ct);
        return TypedResults.NoContent();
    }
}
