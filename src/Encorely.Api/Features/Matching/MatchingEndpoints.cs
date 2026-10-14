using System.Security.Claims;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Encorely.Api.Features.Matching;

public static class MatchingEndpoints
{
    public static void MapMatching(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("").WithTags("Match");

        g.MapPost("/likes", Like).WithName("LikeUser")
            .WithSummary("Like a una persona con la que comparto un evento. Hay match solo si el like es recíproco (idempotente)")
            .Problems(400, 401, 403, 404).UserWrites();
        g.MapGet("/matches", List).WithName("ListMatches").WithSummary("Mis matches activos, el más reciente primero").Problems(401);
        g.MapGet("/matches/{id:guid}", Get).WithName("GetMatch").WithSummary("Un match mío").Problems(401, 404);
        g.MapDelete("/matches/{id:guid}", Unmatch).WithName("Unmatch").WithSummary("Deshace el match y cierra el chat").Problems(401, 404);
    }

    private static async Task<Ok<LikeResponse>> Like(LikeRequest request, ClaimsPrincipal user, MatchingService matching, CancellationToken ct)
        => TypedResults.Ok(await matching.LikeAsync(user.UserId(), request, ct));

    private static async Task<Ok<IReadOnlyList<MatchDto>>> List(ClaimsPrincipal user, MatchingService matching, CancellationToken ct)
        => TypedResults.Ok(await matching.ListAsync(user.UserId(), ct));

    private static async Task<Ok<MatchDto>> Get(Guid id, ClaimsPrincipal user, MatchingService matching, CancellationToken ct)
        => TypedResults.Ok(await matching.GetAsync(user.UserId(), id, ct));

    private static async Task<NoContent> Unmatch(Guid id, ClaimsPrincipal user, MatchingService matching, CancellationToken ct)
    {
        await matching.UnmatchAsync(user.UserId(), id, ct);
        return TypedResults.NoContent();
    }
}
