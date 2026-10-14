using System.Security.Claims;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Encorely.Api.Features.Users;

public static class UserEndpoints
{
    public static void MapUsers(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/me").WithTags("Perfil");

        g.MapGet("", GetMe).WithName("GetMe").WithSummary("Mi perfil").Problems(401);
        g.MapPatch("", UpdateMe).WithName("UpdateMe").WithSummary("Edita mi perfil (campos null no cambian)").Problems(400, 401);
        g.MapDelete("", DeleteMe).WithName("DeleteMe")
            .WithSummary("Borra mi cuenta: datos personales, swipes, likes, matches y mensajes. Irreversible").Problems(401);
        g.MapPost("/consents", AcceptConsent).WithName("AcceptConsent").WithSummary("Acepta una nueva versión de términos y política").Problems(400, 401);
        g.MapGet("/export", Export).WithName("ExportMyData").WithSummary("Descarga mis datos personales (Ley 1581)").Problems(401);
    }

    private static async Task<Ok<MeResponse>> GetMe(ClaimsPrincipal user, UserService users, CancellationToken ct)
        => TypedResults.Ok(await users.GetAsync(user.UserId(), ct));

    private static async Task<Ok<MeResponse>> UpdateMe(UpdateMeRequest request, ClaimsPrincipal user, UserService users, CancellationToken ct)
        => TypedResults.Ok(await users.UpdateAsync(user.UserId(), request, ct));

    private static async Task<NoContent> DeleteMe(ClaimsPrincipal user, UserService users, CancellationToken ct)
    {
        await users.DeleteAsync(user.UserId(), ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> AcceptConsent(ConsentRequest request, ClaimsPrincipal user, UserService users, CancellationToken ct)
    {
        await users.AcceptConsentAsync(user.UserId(), request, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<DataExportResponse>> Export(ClaimsPrincipal user, UserService users, CancellationToken ct)
        => TypedResults.Ok(await users.ExportAsync(user.UserId(), ct));
}
