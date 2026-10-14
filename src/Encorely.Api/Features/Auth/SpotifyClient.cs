using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Http;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Auth;

/// <param name="Email">null si Spotify no lo entrega o no es un correo válido.</param>
public sealed record SpotifyIdentity(string Id, string? Email, string? DisplayName);

/// <summary>
/// Canjea un authorization code de Spotify (PKCE, sin client secret) y lee <c>/v1/me</c>. El access token de Spotify
/// se usa una vez y se descarta: el servidor no guarda tokens de Spotify.
/// </summary>
public sealed class SpotifyClient(HttpClient http, IOptions<SpotifyOptions> options, ILogger<SpotifyClient> logger)
{
    public async Task<SpotifyIdentity> IdentifyAsync(SpotifyGrant grant, CancellationToken ct)
    {
        var spotify = options.Value;
        if (!spotify.IsAllowedRedirect(grant.RedirectUri))
            throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "Revisa los datos enviados.", "redirectUri: no está permitida.");
        if (string.IsNullOrWhiteSpace(spotify.ClientId))
        {
            logger.LogError("SPOTIFY_CLIENT_ID no está configurado: el login con Spotify no funciona");
            throw Unavailable();
        }

        try
        {
            var accessToken = await ExchangeAsync(spotify, grant, ct);
            return await GetProfileAsync(spotify, accessToken, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Spotify no respondió");
            throw Unavailable();
        }
    }

    private async Task<string> ExchangeAsync(SpotifyOptions spotify, SpotifyGrant grant, CancellationToken ct)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = grant.Code,
            ["redirect_uri"] = grant.RedirectUri,
            ["client_id"] = spotify.ClientId!,
            ["code_verifier"] = grant.CodeVerifier,
        });
        using var response = await http.PostAsync(new Uri(new Uri(spotify.AccountsBaseUrl), "api/token"), form, ct);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadFromJsonAsync<TokenError>(ct);
            // invalid_client es configuración nuestra (client id), no culpa del usuario: se trata como caída.
            if (error?.Error == "invalid_client")
            {
                logger.LogError("Spotify rechazó el client id configurado (invalid_client)");
                throw Unavailable();
            }
            throw ApiException.BadRequest(ErrorCodes.SpotifyInvalidCode, "La conexión con Spotify venció. Intenta otra vez.");
        }
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Spotify /api/token respondió {Status}", (int)response.StatusCode);
            throw Unavailable();
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
        return string.IsNullOrEmpty(token?.AccessToken) ? throw Unavailable() : token.AccessToken;
    }

    private async Task<SpotifyIdentity> GetProfileAsync(SpotifyOptions spotify, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(spotify.ApiBaseUrl), "v1/me"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, ct);

        // En modo desarrollo Spotify solo atiende a los usuarios agregados en el Dashboard; al resto le da 403 aquí.
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw ApiException.Forbidden(ErrorCodes.SpotifyUserNotAllowed, "Spotify no permite entrar con esta cuenta todavía.");
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Spotify /v1/me respondió {Status}", (int)response.StatusCode);
            throw Unavailable();
        }

        var me = await response.Content.ReadFromJsonAsync<MeResponse>(ct);
        if (string.IsNullOrWhiteSpace(me?.Id) || me.Id.Length > 128) throw Unavailable();

        var email = Text.Clean(me.Email);
        if (email is not null && (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))) email = null;
        return new SpotifyIdentity(me.Id, email, Text.Clean(me.DisplayName));
    }

    private static ApiException Unavailable()
        => new(StatusCodes.Status502BadGateway, ErrorCodes.SpotifyUnavailable, "Spotify no responde en este momento. Intenta de nuevo en un rato.");

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record TokenError([property: JsonPropertyName("error")] string? Error);

    private sealed record MeResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("display_name")] string? DisplayName);
}
