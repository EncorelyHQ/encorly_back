using Confluent.Kafka;
using EncorelyApplication.Interfaces;
using EncorelyApplication.Services;
using EncorelyInfrastructure.Messaging;
using EncorelyInfrastructure.Persistence;
using EncorelyApi.Hubs;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

builder.Services.AddDbContext<EncorelyDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IEncorelyDbContext>(provider => provider.GetRequiredService<EncorelyDbContext>());

builder.Services.AddSignalR();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ISpotifyService, SpotifyService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICompatibilityService, CompatibilityService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<ISwipeService, SwipeService>();
builder.Services.AddScoped<IEventService, EventService>();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<EncorelyApi.Hubs.VenueHub>("/venueHub");

app.Run();
