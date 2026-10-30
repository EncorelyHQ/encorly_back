using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Encorely.Api.Features.Auth;

public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static void MapAuth(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/auth").WithTags("Auth").AllowAnonymous();

        g.MapPost("/register", Register).WithName("Register").WithSummary("Crea una cuenta con email y contraseña e inicia sesión")
            .RequireRateLimiting(RateLimitPolicy).Problems(400, 409, 429);
        g.MapPost("/login", Login).WithName("Login").WithSummary("Inicia sesión")
            .RequireRateLimiting(RateLimitPolicy).Problems(400, 401, 429);
        g.MapPost("/spotify", SpotifyLogin).WithName("SpotifyLogin")
            .WithSummary("Entra con Spotify (canjea el code PKCE). Crea la cuenta si el Spotify no está vinculado")
            .RequireRateLimiting(RateLimitPolicy).Problems(400, 403, 409, 429, 502);
        g.MapPost("/refresh", Refresh).WithName("Refresh").WithSummary("Rota el refresh token y emite un access token nuevo")
            .RequireRateLimiting(EndpointExtensions.TokensPolicy).Problems(400, 401, 429);
        g.MapPost("/logout", Logout).WithName("Logout").WithSummary("Revoca el refresh token (y su familia)")
            .RequireRateLimiting(EndpointExtensions.TokensPolicy).Problems(400, 429);
    }

    private static async Task<Ok<AuthResponse>> Register(RegisterRequest request, AuthService auth, CancellationToken ct)
        => TypedResults.Ok(await auth.RegisterAsync(request, ct));

    private static async Task<Ok<AuthResponse>> Login(LoginRequest request, AuthService auth, CancellationToken ct)
        => TypedResults.Ok(await auth.LoginAsync(request, ct));

    private static async Task<Ok<SpotifyAuthResponse>> SpotifyLogin(SpotifyLoginRequest request, AuthService auth, CancellationToken ct)
        => TypedResults.Ok(await auth.SpotifyLoginAsync(request, ct));

    private static async Task<Ok<AuthResponse>> Refresh(RefreshRequest request, AuthService auth, CancellationToken ct)
        => TypedResults.Ok(await auth.RefreshAsync(request, ct));

    private static async Task<NoContent> Logout(LogoutRequest request, AuthService auth, CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);
        return TypedResults.NoContent();
    }
}
