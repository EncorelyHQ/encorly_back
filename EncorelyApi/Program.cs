using EncorelyInfrastructure.Hubs;
using EncorelyQuery;
using EncorelyRepository;
using EncorelyQuery.Interfaces;
using EncorelyQuery.Implementations;
using EncorelyRepository.Interfaces;
using EncorelyRepository.Implements;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using EncorelyApplication.Interfaces;
using EncorelyApplication.Services;
using EncorelyInfrastructure.Messaging;
using EncorelyApi.Services;
using Confluent.Kafka;

// Carga el .env del directorio actual (si existe) ANTES de leer cualquier env var.
// En despliegues reales (Render, Docker) las vars vienen del entorno; el .env
// solo es relevante en desarrollo local.
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "Encorely2026!";

var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPass};SSL Mode=Require;Trust Server Certificate=True";

try
{
    if (FirebaseApp.DefaultInstance == null)
    {
        FirebaseApp.Create();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Umbral] Firebase init bypassed (mock environment): {ex.Message}");
}

builder.Services.AddSignalR();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ISpotifyService, SpotifyService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICompatibilityService, CompatibilityService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<ISwipeService, SwipeService>();
builder.Services.AddScoped<IEventService, EventService>();

// Dapper Registrations
builder.Services.AddSingleton<EncorelyQuery.IDbConnectionFactory, EncorelyQuery.DbConnectionFactory>();
builder.Services.AddSingleton<EncorelyRepository.IDbConnectionFactory, EncorelyRepository.DbConnectionFactory>();
builder.Services.AddScoped<IUsuarioQueries, UsuarioQueries>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IMatchQueries, MatchQueries>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<ISwipeQueries, SwipeQueries>();
builder.Services.AddScoped<ISwipeRepository, SwipeRepository>();
builder.Services.AddScoped<IVenueRoomQueries, VenueRoomQueries>();
builder.Services.AddScoped<IVenueRoomRepository, VenueRoomRepository>();
builder.Services.AddScoped<IMusicalProfileQueries, MusicalProfileQueries>();
builder.Services.AddScoped<IMusicalProfileRepository, MusicalProfileRepository>();
builder.Services.AddScoped<IMessageQueries, MessageQueries>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IVenueMessageQueries, VenueMessageQueries>();
builder.Services.AddScoped<IVenueMessageRepository, VenueMessageRepository>();

var kafkaPort = Environment.GetEnvironmentVariable("KAFKA_PORT") ?? "9092";
var kafkaHost = Environment.GetEnvironmentVariable("KAFKA_HOST") ?? "localhost";

var producerConfig = new ProducerConfig
{
    BootstrapServers = $"{kafkaHost}:{kafkaPort}",
    Acks = Acks.All
};

builder.Services.AddSingleton(producerConfig);
builder.Services.AddSingleton(typeof(IKafkaProducer<>), typeof(KafkaProducer<>));
builder.Services.AddSingleton(typeof(IEventProducer<>), typeof(KafkaProducer<>));
builder.Services.AddScoped<IMatchNotificationService, EncorelyApi.Services.SignalRNotificationService>();
builder.Services.AddScoped<IPushNotificationService, FirebasePushNotificationService>();
builder.Services.AddScoped<IVenueService, VenueService>();
builder.Services.AddScoped<IPlaylistService, PlaylistService>();

var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = $"{redisHost}:6379";
    options.InstanceName = "Encorely_";
});

// CORS: permite que el frontend (Expo web / Vercel) consuma la API desde el navegador.
const string FrontendCorsPolicy = "AllowFrontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.SetIsOriginAllowed(_ => true) // dev: cualquier origen. En prod, restringir a tu dominio.
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Encorely Social API", Version = "v1" });
});

var secretKey = builder.Configuration["JWT_SECRET_KEY"] ?? "This_Is_A_Very_Long_Secret_Key_For_Encorely_JWT_2026";
var issuer = builder.Configuration["JWT_ISSUER"] ?? "Encorely.Api";
var audience = builder.Configuration["JWT_AUDIENCE"] ?? "Encorely.Clients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<EncorelyApi.Middleware.ExceptionMiddleware>();

app.UseCors(FrontendCorsPolicy);

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Encorely API v1");
    c.RoutePrefix = "docs";
});

if (app.Environment.IsDevelopment())
{
    // development only logic here if any
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Health check (liveness + DB readiness). Anónimo: útil para monitores externos (Render, UptimeRobot)
// y para diagnosticar la conexión a la base de datos (expone el error real de Postgres).
app.MapGet("/health", async (EncorelyQuery.IDbConnectionFactory dbFactory) =>
{
    var dbStatus = new Dictionary<string, object?> { ["connected"] = false };
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        using var conn = dbFactory.CreateConnection();
        if (conn is System.Data.Common.DbConnection asyncConn)
            await asyncConn.OpenAsync();
        else
            conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        cmd.ExecuteScalar();

        dbStatus["connected"] = true;
    }
    catch (Exception ex)
    {
        dbStatus["error"] = ex.Message;
    }
    finally
    {
        sw.Stop();
        dbStatus["latencyMs"] = sw.ElapsedMilliseconds;
    }

    var connected = (bool)dbStatus["connected"]!;
    var payload = new
    {
        status = connected ? "healthy" : "degraded",
        timestamp = DateTime.UtcNow,
        database = dbStatus
    };

    // Vivo siempre devuelve 200 a nivel de app; 503 si la BD no responde (readiness).
    return connected ? Results.Ok(payload) : Results.Json(payload, statusCode: 503);
}).AllowAnonymous();

app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<VenueHub>("/venueHub");

app.Run();
