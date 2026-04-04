using Confluent.Kafka;
using EncorelyApplication.Interfaces;
using EncorelyApplication.Services;
using EncorelyInfrastructure.Messaging;
using EncorelyInfrastructure.Persistence;
using EncorelyApi.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "encorely_db";
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "admin";
var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "Encorely2026!";

var connectionString = $"Host={dbHost};Database={dbName};Username={dbUser};Password={dbPass}";

builder.Services.AddDbContext<EncorelyDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSignalR();
builder.Services.AddScoped<IIdentityService, IdentityService>();
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");

app.Run();
