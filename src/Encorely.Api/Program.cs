using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Encorely.Api.Data;
using Encorely.Api.Features.Admin;
using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Chat;
using Encorely.Api.Features.Devices;
using Encorely.Api.Features.Events;
using Encorely.Api.Features.Legal;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Profiles;
using Encorely.Api.Features.Sessions;
using Encorely.Api.Features.Sync;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Encorely.Api.Infrastructure.Push;
using Encorely.Api.Infrastructure.Realtime;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

DotEnv.LoadFromNearest(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEncorelyEnvironment();

// Configuración validada al arrancar: si falta un secreto, la API no levanta (sin valores de respaldo).
builder.Services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
builder.Services.AddOptions<ProxyOptions>().BindConfiguration(ProxyOptions.Section).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ProxyOptions>, ProxyOptionsValidator>();
builder.Services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ProxyOptions>>((o, proxy) =>
{
    var networks = proxy.Value.ParseNetworks();
    if (networks.Count == 0) return;
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    // Por defecto el middleware confía en loopback: se reemplaza por exactamente las redes configuradas.
    o.KnownProxies.Clear();
    o.KnownIPNetworks.Clear();
    foreach (var n in networks) o.KnownIPNetworks.Add(n);
});
builder.Services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<MatchingOptions>().BindConfiguration(MatchingOptions.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<RateLimitOptions>().BindConfiguration(RateLimitOptions.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<ChatOptions>().BindConfiguration(ChatOptions.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.Section).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SeedOptions>, SeedOptionsValidator>();
builder.Services.AddOptions<PushOptions>().BindConfiguration(PushOptions.Section);
builder.Services.AddOptions<LegalOptions>().BindConfiguration(LegalOptions.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<LegalOptions>, LegalOptionsValidator>();
builder.Services.AddOptions<SpotifyOptions>().BindConfiguration(SpotifyOptions.Section);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>((sp, o) => o
    .UseNpgsql(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString)
    .UseSnakeCaseNamingConvention());

builder.Services.ConfigureHttpJsonOptions(o => ConfigureJson(o.SerializerOptions));
builder.Services.AddValidation();
builder.Services.AddEncorelyProblemDetails();
builder.Services.AddEncorelyAuth();
builder.Services.AddEncorelyOpenApi();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
builder.Services.AddSignalR().AddJsonProtocol(o => ConfigureJson(o.PayloadSerializerOptions));
builder.Services.AddSingleton<IUserIdProvider, SubUserIdProvider>();
builder.Services.AddSingleton<HubConnections>();
builder.Services.AddScoped<Notifier>();
builder.Services.AddHttpClient();
builder.Services.AddExpoPush();
builder.Services.AddSingleton<IPushSender>(sp => sp.GetRequiredService<IOptions<PushOptions>>().Value.Enabled
    ? ActivatorUtilities.CreateInstance<ExpoPushSender>(sp)
    : new NoopPushSender());

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<LegalService>();
builder.Services.AddScoped<MusicService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<RadarService>();
builder.Services.AddScoped<MatchingService>();
builder.Services.AddScoped<KeyService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<ModerationService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddSingleton<MaintenanceService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MaintenanceService>());
builder.Services.AddHttpClient<ITunesClient>(c =>
{
    c.BaseAddress = new Uri(ITunesClient.BaseAddress);
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<SpotifyClient>(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<Seeder>();

builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy(AuthEndpoints.RateLimitPolicy, ctx =>
    {
        var limits = ctx.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limits.AuthPermitPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
    o.AddPolicy(EndpointExtensions.UserWritesPolicy, ctx =>
    {
        var limits = ctx.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        var key = ctx.User.TryUserId()?.ToString() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = limits.UserWritesPerMinute, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0,
        });
    });
    o.AddPolicy(EndpointExtensions.ReportsPolicy, ctx =>
    {
        var limits = ctx.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        var key = ctx.User.TryUserId()?.ToString() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = limits.ReportsPerHour, Window = TimeSpan.FromHours(1), SegmentsPerWindow = 6, QueueLimit = 0,
        });
    });
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
        await ctx.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx.HttpContext,
            ProblemDetails = new ProblemDetails { Status = 429 },
        });
    };
});

var corsOrigins = builder.Configuration["Cors:Origins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);

var app = builder.Build();

// Primero: el rate limit y el log de acceso deben ver la IP real del cliente, y solo si el proxy es de confianza.
if (!string.IsNullOrWhiteSpace(app.Configuration[$"{ProxyOptions.Section}:{nameof(ProxyOptions.TrustedNetworks)}"]))
    app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSecurityHeaders();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseSafeRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseActiveUserCheck();
app.UseAuthorization();
app.UseRateLimiter();

// El contrato no se publica en producción (mapa completo de la superficie de ataque); Testing lo necesita para el test de contrato.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
else
    // Sin ruta, la política por defecto respondería 401 y delataría que el recurso existe en otros entornos.
    app.MapGet("/openapi/{**path}", () => Results.NotFound()).AllowAnonymous().ExcludeFromDescription();
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "Encorely v1");
        o.RoutePrefix = "swagger";
    });
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

var api = app.MapGroup("/api/v1");
api.MapAuth();
api.MapLegal();
api.MapUsers();
api.MapProfiles();
api.MapMusic();
api.MapEvents();
api.MapMatching();
api.MapChat();
api.MapModeration();
api.MapDevices();
api.MapSync();
api.MapSessions();
api.MapAdmin();
// Una conexión no sobrevive al vencimiento del JWT con el que se abrió: el cliente reconecta con uno fresco.
app.MapHub<ChatHub>(ChatHub.Path, o => o.CloseOnAuthenticationExpiration = true);
app.MapAccountDeletionPage();
app.MapAdminPanel();
// Los navegadores lo piden solos; sin esto la política por defecto respondería 401.
app.MapGet("/favicon.ico", () => Results.NoContent()).AllowAnonymous().ExcludeFromDescription();

await app.Services.PrepareDatabaseAsync();
app.Run();

static void ConfigureJson(JsonSerializerOptions o)
{
    o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.NumberHandling = JsonNumberHandling.Strict;
    o.Converters.Add(new NoNulCharStringConverter());
    o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    // Campos obligatorios de verdad: faltar uno (o mandar null donde no se admite) es 400, no un default silencioso.
    o.RespectNullableAnnotations = true;
    o.RespectRequiredConstructorParameters = true;
}

public partial class Program;
