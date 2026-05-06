using EncorelyApplication.DTOs;
using EncorelyApplication.Interfaces;
using EncorelyModels;
using EncorelyDomain.Events;
using Microsoft.Extensions.Logging;
using EncorelyQuery.Interfaces;
using EncorelyRepository.Interfaces;

namespace EncorelyApplication.Services;

public class AuthService : IAuthService
{
    private readonly IUsuarioQueries _usuarioQueries;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IKafkaProducer<UserSyncEvent> _kafkaProducer;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUsuarioQueries usuarioQueries,
        IUsuarioRepository usuarioRepository,
        IKafkaProducer<UserSyncEvent> kafkaProducer,
        ILogger<AuthService> logger)
    {
        _usuarioQueries = usuarioQueries;
        _usuarioRepository = usuarioRepository;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    public async Task<Guid> AuthenticateWithSpotifyAsync(SpotifyAuthRequest authRequest, CancellationToken ct = default)
    {
        // 1. Mock Spotify Profile Data
        var spotifyId = "spotify_id_123";
        var displayName = "Spotify Explorer";
        var email = "explorer@spotify.com";

        // 2. Check if user exists
        var user = await _usuarioQueries.GetBySpotifyIdAsync(spotifyId);

        if (user == null)
        {
            user = new Usuario
            {
                Id = Guid.NewGuid(),
                SpotifyId = spotifyId,
                DisplayName = displayName,
                Email = email,
                CreatedAt = DateTime.UtcNow
            };

            await _usuarioRepository.CreateAsync(user);
            
            _logger.LogInformation("New user registered: {UserId} ({SpotifyId})", user.Id, spotifyId);
        }

        // 3. Emit UserSyncEvent to Kafka
        var syncEvent = new UserSyncEvent(user.Id, authRequest.AccessToken, DateTime.UtcNow);
        
        await _kafkaProducer.ProduceAsync(KafkaTopics.UserDnaSync, syncEvent, ct);
        
        _logger.LogInformation("UserSyncEvent sent to Kafka for user: {UserId}", user.Id);

        return user.Id;
    }
}
