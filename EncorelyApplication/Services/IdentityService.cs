using EncorelyApplication.Interfaces;
using EncorelyDomain.Entities;
using EncorelyDomain.Events;
using EncorelyApplication.DTOs.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EncorelyApplication.Services;

public class IdentityService : IIdentityService
{
    // Persistence via Entity Framework Core

    private readonly IEncorelyDbContext _dbContext;
    private readonly IKafkaProducer<UserSyncEvent> _kafkaProducer;
    private readonly ITokenService _tokenService;
    private readonly ISpotifyService _spotifyService;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        IEncorelyDbContext dbContext,
        IKafkaProducer<UserSyncEvent> kafkaProducer,
        ITokenService tokenService,
        ISpotifyService spotifyService,
        ILogger<IdentityService> logger)
    {
        _dbContext = dbContext;
        _kafkaProducer = kafkaProducer;
        _tokenService = tokenService;
        _spotifyService = spotifyService;
        _logger = logger;
    }

    public async Task<TokenResponse> LoginWithSpotifyAsync(string accessToken, CancellationToken ct = default)
    {
        var (spotifyId, email, displayName) = await _spotifyService.GetUserProfileAsync(accessToken, ct);
        
        return await ProcessIdentityAsync(spotifyId, email ?? $"{spotifyId}@spotify.com", AuthProvider.Spotify, accessToken, ct, null, displayName);
    }

    public async Task<TokenResponse> LoginWithGoogleAsync(string idToken, CancellationToken ct = default)
    {
        // Mock Google Profile
        var googleId = "google_" + Guid.NewGuid().ToString().Substring(0, 8);
        var email = $"{googleId}@google.com";
        
        return await ProcessIdentityAsync(googleId, email, AuthProvider.Google, null, ct);
    }

    public async Task<TokenResponse> RegisterWithEmailAsync(string email, string password, CancellationToken ct = default)
    {
        if (await _dbContext.Users.AnyAsync(u => u.Email == email, ct))
            throw new Exception("User already exists");

        return await ProcessIdentityAsync(email, email, AuthProvider.Custom, null, ct, password);
    }

    public async Task<TokenResponse> LoginWithEmailAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == password, ct);
        if (user == null) throw new Exception("Invalid credentials");
        
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _dbContext.SaveChangesAsync(ct);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Expiration = DateTime.UtcNow.AddMinutes(30),
            UserId = user.Id
        };
    }

    private async Task<TokenResponse> ProcessIdentityAsync(
        string providerId, 
        string email, 
        AuthProvider provider, 
        string? token, 
        CancellationToken ct,
        string? password = null,
        string? displayName = null)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.SpotifyId == providerId || u.Email == email, ct);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                SpotifyId = providerId,
                DisplayName = displayName ?? email.Split('@')[0],
                Email = email,
                Provider = provider,
                PasswordHash = password, // Simplified hash for testing
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Users.AddAsync(user, ct);
            
            MusicalProfile profile;
            if (provider == AuthProvider.Spotify && token != null)
            {
                profile = await _spotifyService.GenerateMusicalProfileAsync(token, ct);
                profile.Id = Guid.NewGuid();
                profile.UserId = user.Id;
            }
            else
            {
                profile = new MusicalProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Energy = 0.5,
                    Danceability = 0.5,
                    Valence = 0.5,
                    Tempo = 120
                };
            }

            await _dbContext.MusicalProfiles.AddAsync(profile, ct);

            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("New user registered via {Provider}: {UserId}", provider, user.Id);
        }

        // Tarea 1 logic: Emit Kafka event for Spotify users
        if (provider == AuthProvider.Spotify && token != null)
        {
            await _kafkaProducer.ProduceAsync(KafkaTopics.UserDnaSync, new UserSyncEvent(user.Id, token, DateTime.UtcNow), ct);
        }

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _dbContext.SaveChangesAsync(ct);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Expiration = DateTime.UtcNow.AddMinutes(30),
            UserId = user.Id
        };
    }
}
