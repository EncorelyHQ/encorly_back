using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Encorely.Api.Infrastructure.Auth;

public static class AuthSetup
{
    public const string AdminPolicy = "admin";

    public static IServiceCollection AddEncorelyAuth(this IServiceCollection services)
    {
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<TokenService>();
        services.AddScoped<SessionRevoker>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((o, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = TokenService.SigningKey(jwt),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                };
                o.Events = new JwtBearerEvents
                {
                    // WebSockets no permiten cabeceras: el token llega por query SOLO en /hubs/*.
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AdminPolicy, p => p.RequireAuthenticatedUser().RequireRole("admin"));

        return services;
    }

    /// <summary>
    /// Un JWT vigente no basta: la cuenta debe seguir existiendo y no estar suspendida, y la sesión de su <c>sid</c> debe
    /// seguir abierta (borrar o suspender la cuenta, o cerrar la sesión, corta el acceso de inmediato, sin esperar a que venza el token).
    /// </summary>
    public static IApplicationBuilder UseActiveUserCheck(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        if (ctx.User.Identity?.IsAuthenticated == true && ctx.User.TryUserId() is { } userId)
        {
            var db = ctx.RequestServices.GetRequiredService<AppDbContext>();
            var now = ctx.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var sid = ctx.User.SessionId();
            // Una sola consulta: sin sid (tokens anteriores al claim) la sesión cuenta como cerrada.
            var state = await db.Users.Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.Status,
                    SessionOpen = db.RefreshTokens.Any(t => t.FamilyId == sid && t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now),
                })
                .FirstOrDefaultAsync(ctx.RequestAborted);
            if (state is null || state.Status == UserStatus.Banned || !state.SessionOpen)
            {
                var (code, statusCode, title) = state is null || state.Status != UserStatus.Banned
                    ? (ErrorCodes.Unauthorized, 401, "Tu sesión ya no es válida.")
                    : (ErrorCodes.AccountBanned, 403, "Tu cuenta está suspendida.");
                ctx.Response.StatusCode = statusCode;
                var pd = new ProblemDetails { Status = statusCode, Title = title };
                pd.Extensions["code"] = code;
                await ctx.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext { HttpContext = ctx, ProblemDetails = pd });
                return;
            }
        }
        await next();
    });
}
