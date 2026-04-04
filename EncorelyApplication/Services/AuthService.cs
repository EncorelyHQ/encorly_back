using EncorelyApplication.DTOs;
using EncorelyApplication.Interfaces;
using EncorelyDomain.Entities;
using EncorelyDomain.Events;
using EncorelyInfrastructure.Messaging;
using EncorelyInfrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EncorelyApplication.Services;

public class AuthService : IAuthService
{
    private readonly EncorelyDbContext _dbContext;
    private readonly IKafkaProducer<UserSyncEvent> _kafkaProducer;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        EncorelyDbContext dbContext,
        IKafkaProducer<UserSyncEvent> kafkaProducer,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    public async Task<Guid> AuthenticateWithSpotifyAsync(SpotifyAuthRequest authRequest, CancellationToken ct = default)
    {
        // 1. Mock Spotify Profile Data (In a real scenario, call Spotify API)
        var spotifyId = "spotify_id_123";
        var displayName = "Spotify Explorer";
        var email = "explorer@spotify.com";

        // 2. Check if user exists
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.SpotifyId == spotifyId, ct);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                SpotifyId = spotifyId,
                DisplayName = displayName,
                Email = email,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Users.AddAsync(user, ct);
            await _dbContext.SaveChangesAsync(ct);
            
            _logger.LogInformation("New user registered: {UserId} ({SpotifyId})", user.Id, spotifyId);
        }

        // 3. Emit UserSyncEvent to Kafka
        var syncEvent = new UserSyncEvent(user.Id, authRequest.AccessToken, DateTime.UtcNow);
        
        await _kafkaProducer.ProduceAsync(KafkaTopics.UserDnaSync, syncEvent, ct);
        
        _logger.LogInformation("UserSyncEvent sent to Kafka for user: {UserId}", user.Id);

        return user.Id;
    }
}
