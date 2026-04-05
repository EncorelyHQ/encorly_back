using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using EncorelyDomain.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using EncorelyApplication.Interfaces;
using System.Text.Json;

namespace EncorelyWorker;

public class KafkaConsumerWorker : BackgroundService
{
    private readonly ILogger<KafkaConsumerWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;

    public KafkaConsumerWorker(ILogger<KafkaConsumerWorker> logger, IConfiguration configuration, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = "encorely-processor-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(new[] { "swipe-raw-events" });

        _logger.LogInformation("Encorely Worker started. Listening for 'swipe-raw-events'...");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var consumeResult = consumer.Consume(stoppingToken);
                var swipeEvent = JsonSerializer.Deserialize<SwipeRegisteredEvent>(consumeResult.Message.Value);
                
                if (swipeEvent != null)
                {
                    _logger.LogInformation("[WORKER] Procesando Swipe de Usuario {UserId} para Track {TrackId}", swipeEvent.UserId, swipeEvent.TrackId);
                    
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<IEncorelyDbContext>();
                    var spotifyService = scope.ServiceProvider.GetRequiredService<ISpotifyService>();
                    var hubContext = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<EncorelyInfrastructure.Hubs.NotificationHub>>();
                    
                    var user = await dbContext.Users.FindAsync(new object[] { swipeEvent.UserId }, stoppingToken);
                    if (user != null)
                    {
                        user.SwipeCount++;
                        await dbContext.SaveChangesAsync(stoppingToken);
                        
                        _logger.LogInformation("[WORKER] Usuario {UserId} Swipe Count: {Count}", user.Id, user.SwipeCount);

                        if (user.SwipeCount == 25)
                        {
                            _logger.LogInformation("[DNA_COMPLETED] Generando perfil real para usuario {UserId}...", user.Id);
                            
                            // Simular obtención de token (en un sistema real se obtendría de la sesión/DB)
                            var mockToken = "spotify_access_token_from_db"; 
                            var profile = await spotifyService.GenerateMusicalProfileAsync(mockToken, stoppingToken);
                            profile.UserId = user.Id;

                            var existingProfile = await dbContext.MusicalProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, stoppingToken);
                            if (existingProfile != null)
                            {
                                existingProfile.Energy = profile.Energy;
                                existingProfile.Danceability = profile.Danceability;
                                existingProfile.Valence = profile.Valence;
                                existingProfile.Tempo = profile.Tempo;
                            }
                            else
                            {
                                await dbContext.MusicalProfiles.AddAsync(profile, stoppingToken);
                            }

                            await dbContext.SaveChangesAsync(stoppingToken);

                            // Notificar vía SignalR
                            await hubContext.Clients.Group(user.Id.ToString())
                                .SendAsync("DnaCompleted", new { UserId = user.Id, Message = "¡Tu ADN musical está listo! Ya puedes usar el Radar." });
                            
                            _logger.LogInformation("[DNA_COMPLETED] El ADN Musical del usuario {UserId} ha sido actualizado y notificado.", user.Id);
                        }
                    }
                }
                
                await Task.Delay(100, stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            consumer.Close();
        }
    }
}
