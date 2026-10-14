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
        g.MapPost("/refresh", Refresh).WithName("Refresh").WithSummary("Rota el refresh token y emite un access token nuevo")
            .Problems(400, 401);
        g.MapPost("/logout", Logout).WithName("Logout").WithSummary("Revoca el refresh token (y su familia)")
            .Problems(400);
    }

    private static async Task<Ok<AuthResponse>> Register(RegisterRequest request, AuthService auth, CancellationToken ct)
        => TypedResults.Ok(await auth.RegisterAsync(request, ct));

    private static async Task<Ok<AuthResponse>> Login(LoginRequest request, AuthService auth, CancellationToken ct)
        => TypedResults.Ok(await auth.LoginAsync(request, ct));

    private static async Task<Ok<AuthResponse>> Refresh(RefreshRequest request, AuthService auth, CancellationToken ct)
        => TypedResults.Ok(await auth.RefreshAsync(request, ct));

    private static async Task<NoContent> Logout(LogoutRequest request, AuthService auth, CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);
        return TypedResults.NoContent();
    }
}
