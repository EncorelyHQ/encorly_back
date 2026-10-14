using Encorely.Api.Features.Auth;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Encorely.Api.Features.Legal;

public static class LegalEndpoints
{
    public static void MapLegal(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("").WithTags("Legal").AllowAnonymous();

        g.MapGet("/legal/current", GetCurrent).WithName("GetLegal").WithSummary("Versión vigente de términos/política y URLs públicas");
        g.MapPost("/account-deletion", DeleteAccount).WithName("DeleteAccountWeb")
            .WithSummary("Borra la cuenta con email y contraseña (página web de borrado, sin la app)")
            .RequireRateLimiting(AuthEndpoints.RateLimitPolicy).Problems(400, 401, 429);
    }

    private static Ok<LegalResponse> GetCurrent(LegalService legal) => TypedResults.Ok(legal.Current());

    private static async Task<NoContent> DeleteAccount(AccountDeletionRequest request, LegalService legal, CancellationToken ct)
    {
        await legal.DeleteAccountAsync(request, ct);
        return TypedResults.NoContent();
    }
}
