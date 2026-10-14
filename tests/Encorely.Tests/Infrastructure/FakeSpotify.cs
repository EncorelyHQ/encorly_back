using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Encorely.Api.Features.Auth;

namespace Encorely.Tests.Infrastructure;

/// <summary>
/// Spotify falso (accounts + Web API) detrás del HttpClient de la API. Cada code es de un solo uso y solo se canjea con
/// el client id, la redirect URI y el verifier de las pruebas, como el real.
/// </summary>
public sealed class FakeSpotify
{
    public const string ClientId = "spotify-client-de-pruebas";
    public const string RedirectUri = "encorely://spotify-callback";
    public static readonly string Verifier = new('v', 43);

    /// <param name="TokenStatus">Si no es 200, /api/token responde ese status.</param>
    /// <param name="MeStatus">Si no es 200, /v1/me responde ese status.</param>
    public sealed record Account(
        string Id, string? Email, string? DisplayName = "Ana Spotify",
        HttpStatusCode TokenStatus = HttpStatusCode.OK, HttpStatusCode MeStatus = HttpStatusCode.OK);

    private readonly ConcurrentDictionary<string, Account> _codes = new();
    private readonly ConcurrentDictionary<string, Account> _tokens = new();
    private int _requests;

    public int Requests => _requests;

    public static Account NewAccount(string? email = null) => new($"sp{Guid.NewGuid():N}", email ?? $"{Guid.NewGuid():N}@spotify.test");

    /// <summary>Emite un code para la cuenta y devuelve el grant que mandaría la app.</summary>
    public SpotifyGrant Grant(Account account)
    {
        var code = $"code-{Guid.NewGuid():N}";
        _codes[code] = account;
        return new SpotifyGrant(code, Verifier, RedirectUri);
    }

    public HttpMessageHandler CreateHandler() => new Handler(this);

    private sealed class Handler(FakeSpotify spotify) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref spotify._requests);
            return request.RequestUri!.AbsolutePath switch
            {
                "/api/token" when request.Method == HttpMethod.Post => await TokenAsync(request, ct),
                "/v1/me" when request.Method == HttpMethod.Get => Me(request),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        private async Task<HttpResponseMessage> TokenAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var form = (await request.Content!.ReadAsStringAsync(ct)).Split('&')
                .Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
            if (form.GetValueOrDefault("client_id") != ClientId)
                return Json(HttpStatusCode.BadRequest, new { error = "invalid_client" });
            if (form.GetValueOrDefault("grant_type") != "authorization_code"
                || form.GetValueOrDefault("redirect_uri") != RedirectUri
                || form.GetValueOrDefault("code_verifier") != Verifier
                || !spotify._codes.TryRemove(form.GetValueOrDefault("code") ?? "", out var account))
                return Json(HttpStatusCode.BadRequest, new { error = "invalid_grant", error_description = "Invalid authorization code" });
            if (account.TokenStatus != HttpStatusCode.OK) return new HttpResponseMessage(account.TokenStatus);

            var token = $"tok-{Guid.NewGuid():N}";
            spotify._tokens[token] = account;
            return Json(HttpStatusCode.OK, new { access_token = token, token_type = "Bearer", expires_in = 3600 });
        }

        private HttpResponseMessage Me(HttpRequestMessage request)
        {
            if (request.Headers.Authorization?.Parameter is not { } token || !spotify._tokens.TryGetValue(token, out var account))
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            if (account.MeStatus != HttpStatusCode.OK) return new HttpResponseMessage(account.MeStatus);
            return Json(HttpStatusCode.OK, new { id = account.Id, email = account.Email, display_name = account.DisplayName });
        }

        private static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status) { Content = JsonContent.Create(body) };
    }
}
