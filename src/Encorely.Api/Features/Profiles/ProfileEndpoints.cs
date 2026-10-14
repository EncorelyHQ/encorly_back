using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Encorely.Api.Features.Profiles;

public static class ProfileEndpoints
{
    public static void MapProfiles(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/users/{id:guid}").WithTags("Perfil social");

        g.MapGet("/profile", GetProfile).WithName("GetUserProfile")
            .WithSummary("Perfil de otra persona (match activo o evento próximo en común); 404 si no se puede ver").Problems(401, 404);
        g.MapPut("/reactions", SetReaction).WithName("SetProfileReaction")
            .WithSummary("Reacciona a un artista o género de su perfil (reemplaza la reacción anterior a ese objetivo)")
            .UserWrites().Problems(400, 401, 404);
        g.MapDelete("/reactions", RemoveReaction).WithName("RemoveProfileReaction")
            .WithSummary("Quita mi reacción a ese objetivo (204 aunque no hubiera)").UserWrites().Problems(400, 401);
    }

    private static async Task<Ok<PublicProfileDto>> GetProfile(Guid id, ClaimsPrincipal user, ProfileService profiles, CancellationToken ct)
        => TypedResults.Ok(await profiles.GetAsync(user.UserId(), id, ct));

    private static async Task<NoContent> SetReaction(Guid id, ReactionRequest request, ClaimsPrincipal user, ProfileService profiles, CancellationToken ct)
    {
        await profiles.SetReactionAsync(user.UserId(), id, request, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RemoveReaction(
        Guid id,
        [Description("artist o genre")][AllowedValues("artist", "genre")] string targetKind,
        [Description("Slug del género o key del artista")][MaxLength(100)] string targetKey,
        ClaimsPrincipal user, ProfileService profiles, CancellationToken ct)
    {
        await profiles.RemoveReactionAsync(user.UserId(), id, Enum.Parse<ReactionTargetKind>(targetKind, ignoreCase: true), targetKey, ct);
        return TypedResults.NoContent();
    }
}
