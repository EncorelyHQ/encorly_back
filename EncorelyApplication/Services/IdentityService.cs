using EncorelyApplication.Interfaces;
using EncorelyDomain.Entities;
using EncorelyDomain.Events;
using EncorelyInfrastructure.Messaging;
using EncorelyInfrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EncorelyApplication.Services;

public class IdentityService : IIdentityService
{
    // Persistence in Memory for rapid testing (Tarea 5)
    private static readonly List<User> _usersMemory = new();
    private static readonly List<MusicalProfile> _profilesMemory = new();

    private readonly EncorelyDbContext _dbContext;
    private readonly IKafkaProducer<UserSyncEvent> _kafkaProducer;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        EncorelyDbContext dbContext,
        IKafkaProducer<UserSyncEvent> kafkaProducer,
        ILogger<IdentityService> logger)
    {
        _dbContext = dbContext;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    public async Task<Guid> LoginWithSpotifyAsync(string accessToken, CancellationToken ct = default)
    {
        // Mock Spotify Profile
        var spotifyId = "spotify_" + Guid.NewGuid().ToString().Substring(0, 8);
        var email = $"{spotifyId}@spotify.com";
        
        return await ProcessIdentityAsync(spotifyId, email, AuthProvider.Spotify, accessToken, ct);
    }

    public async Task<Guid> LoginWithGoogleAsync(string idToken, CancellationToken ct = default)
    {
        // Mock Google Profile
        var googleId = "google_" + Guid.NewGuid().ToString().Substring(0, 8);
        var email = $"{googleId}@google.com";
        
        return await ProcessIdentityAsync(googleId, email, AuthProvider.Google, null, ct);
    }

    public async Task<Guid> RegisterWithEmailAsync(string email, string password, CancellationToken ct = default)
    {
        if (_usersMemory.Any(u => u.Email == email))
            throw new Exception("User already exists");

        return await ProcessIdentityAsync(email, email, AuthProvider.Custom, null, ct, password);
    }

    public async Task<Guid> LoginWithEmailAsync(string email, string password, CancellationToken ct = default)
    {
        var user = _usersMemory.FirstOrDefault(u => u.Email == email && u.PasswordHash == password);
        if (user == null) throw new Exception("Invalid credentials");
        
        return user.Id;
    }

    private async Task<Guid> ProcessIdentityAsync(
        string providerId, 
        string email, 
        AuthProvider provider, 
        string? token, 
        CancellationToken ct,
        string? password = null)
    {
        var user = _usersMemory.FirstOrDefault(u => u.SpotifyId == providerId || u.Email == email);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                SpotifyId = providerId,
                DisplayName = email.Split('@')[0],
                Email = email,
                Provider = provider,
                PasswordHash = password, // Simplified hash for testing
                CreatedAt = DateTime.UtcNow
            };

            _usersMemory.Add(user);
            
            // Tarea 1 logic: Create basic MusicalProfile
            _profilesMemory.Add(new MusicalProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Energy = 0.5,
                Danceability = 0.5,
                Valence = 0.5,
                Tempo = 120
            });

            _logger.LogInformation("New user registered via {Provider}: {UserId}", provider, user.Id);
        }

        // Tarea 1 logic: Emit Kafka event for Spotify users
        if (provider == AuthProvider.Spotify && token != null)
        {
            await _kafkaProducer.ProduceAsync(KafkaTopics.UserDnaSync, new UserSyncEvent(user.Id, token, DateTime.UtcNow), ct);
        }

        return user.Id;
    }
}
