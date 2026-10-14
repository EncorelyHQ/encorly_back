using System.Security.Claims;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Encorely.Api.Features.Moderation;

public static class ModerationEndpoints
{
    public static void MapModeration(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("").WithTags("Moderación");

        g.MapPost("/blocks", BlockUser).WithName("BlockUser")
            .WithSummary("Bloquea a un usuario: cierra el match, desaparece del radar y no puede escribirme (idempotente)")
            .Problems(400, 401, 404).UserWrites();
        g.MapGet("/blocks", ListBlocks).WithName("ListBlocks").WithSummary("Usuarios que bloqueé").Problems(401);
        g.MapDelete("/blocks/{userId:guid}", Unblock).WithName("UnblockUser").WithSummary("Desbloquea (no reabre el match)").Problems(401);
        g.MapPost("/reports", ReportUser).WithName("ReportUser").WithSummary("Reporta a un usuario para revisión").Problems(400, 401, 404).UserWrites();
    }

    private static async Task<NoContent> BlockUser(BlockRequest request, ClaimsPrincipal user, ModerationService moderation, CancellationToken ct)
    {
        await moderation.BlockAsync(user.UserId(), request, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<BlockedUserDto>>> ListBlocks(ClaimsPrincipal user, ModerationService moderation, CancellationToken ct)
        => TypedResults.Ok(await moderation.ListBlocksAsync(user.UserId(), ct));

    private static async Task<NoContent> Unblock(Guid userId, ClaimsPrincipal user, ModerationService moderation, CancellationToken ct)
    {
        await moderation.UnblockAsync(user.UserId(), userId, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Accepted<ReportCreatedDto>> ReportUser(ReportRequest request, ClaimsPrincipal user, ModerationService moderation, CancellationToken ct)
        => TypedResults.Accepted((string?)null, await moderation.ReportAsync(user.UserId(), request, ct));
}
