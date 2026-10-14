using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Encorely.Api.Data;
using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Chat;
using Encorely.Api.Features.Devices;
using Encorely.Api.Features.Events;
using Encorely.Api.Features.Legal;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Encorely.Api.Infrastructure.Push;
using Encorely.Api.Infrastructure.Realtime;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
builder.Services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<MatchingOptions>().BindConfiguration(MatchingOptions.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<RateLimitOptions>().BindConfiguration(RateLimitOptions.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.Section);
builder.Services.AddOptions<PushOptions>().BindConfiguration(PushOptions.Section);
builder.Services.AddOptions<LegalOptions>().BindConfiguration(LegalOptions.Section).ValidateDataAnnotations().ValidateOnStart();

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
builder.Services.AddScoped<Notifier>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IPushSender>(sp => sp.GetRequiredService<IOptions<PushOptions>>().Value.Enabled
    ? ActivatorUtilities.CreateInstance<ExpoPushSender>(sp)
    : new NoopPushSender());

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<LegalService>();
builder.Services.AddScoped<MusicService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<RadarService>();
builder.Services.AddScoped<MatchingService>();
builder.Services.AddScoped<KeyService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<ModerationService>();
builder.Services.AddScoped<DeviceService>();
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

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
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
api.MapMusic();
api.MapEvents();
api.MapMatching();
api.MapChat();
api.MapModeration();
api.MapDevices();
app.MapHub<ChatHub>(ChatHub.Path);
app.MapAccountDeletionPage();

await app.Services.PrepareDatabaseAsync();
app.Run();

static void ConfigureJson(JsonSerializerOptions o)
{
    o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.NumberHandling = JsonNumberHandling.Strict;
    o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    // Campos obligatorios de verdad: faltar uno (o mandar null donde no se admite) es 400, no un default silencioso.
    o.RespectNullableAnnotations = true;
    o.RespectRequiredConstructorParameters = true;
}

public partial class Program;
