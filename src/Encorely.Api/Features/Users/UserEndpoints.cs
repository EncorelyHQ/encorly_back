using System.Security.Claims;
using Encorely.Api.Features.Auth;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Encorely.Api.Features.Users;

public static class UserEndpoints
{
    public static void MapUsers(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/me").WithTags("Perfil");

        g.MapGet("", GetMe).WithName("GetMe").WithSummary("Mi perfil").Problems(401);
        g.MapPatch("", UpdateMe).WithName("UpdateMe").WithSummary("Edita mi perfil (campos null no cambian)").Problems(400, 401);
        g.MapDelete("", DeleteMe).WithName("DeleteMe")
            .WithSummary("Borra mi cuenta (pide la contraseña o reconfirmar con Spotify): datos personales, swipes, likes, matches y mensajes. Irreversible")
            .RequireRateLimiting(AuthEndpoints.RateLimitPolicy).Problems(400, 401, 403, 429, 502);
        g.MapPost("/spotify/link", LinkSpotify).WithName("LinkSpotify").WithSummary("Vincula mi cuenta de Spotify para poder entrar con ella")
            .RequireRateLimiting(AuthEndpoints.RateLimitPolicy).Problems(400, 401, 403, 409, 429, 502);
        g.MapDelete("/spotify/link", UnlinkSpotify).WithName("UnlinkSpotify").WithSummary("Desvincula Spotify (no si es mi única forma de entrar)")
            .Problems(401, 409);
        g.MapPost("/consents", AcceptConsent).WithName("AcceptConsent").WithSummary("Acepta una nueva versión de términos y política").Problems(400, 401);
        g.MapGet("/export", Export).WithName("ExportMyData").WithSummary("Descarga mis datos personales (Ley 1581)").Problems(401).UserWrites();
    }

    private static async Task<Ok<MeResponse>> GetMe(ClaimsPrincipal user, UserService users, CancellationToken ct)
        => TypedResults.Ok(await users.GetAsync(user.UserId(), ct));

    private static async Task<Ok<MeResponse>> UpdateMe(UpdateMeRequest request, ClaimsPrincipal user, UserService users, CancellationToken ct)
        => TypedResults.Ok(await users.UpdateAsync(user.UserId(), request, ct));

    private static async Task<NoContent> DeleteMe([FromBody] DeleteMeRequest request, ClaimsPrincipal user, AuthService auth, UserService users, CancellationToken ct)
    {
        var userId = user.UserId();
        if (string.IsNullOrEmpty(request.Password) == (request.Spotify is null))
            throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "Revisa los datos enviados.", "Envía tu contraseña o reconfirma con Spotify (solo uno).");

        if (request.Spotify is { } grant) await auth.VerifySpotifyAsync(userId, grant, ct);
        else await auth.VerifyPasswordAsync(userId, request.Password!, ct);
        await users.DeleteAsync(userId, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> LinkSpotify(SpotifyGrant request, ClaimsPrincipal user, AuthService auth, CancellationToken ct)
    {
        await auth.LinkSpotifyAsync(user.UserId(), request, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UnlinkSpotify(ClaimsPrincipal user, AuthService auth, CancellationToken ct)
    {
        await auth.UnlinkSpotifyAsync(user.UserId(), ct);
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
