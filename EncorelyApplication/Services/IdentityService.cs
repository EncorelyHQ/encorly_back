using EncorelyApplication.Interfaces;
using EncorelyModels;
using EncorelyDomain.Events;
using EncorelyApplication.DTOs.Auth;
using Microsoft.Extensions.Logging;
using EncorelyQuery.Interfaces;
using EncorelyRepository.Interfaces;

namespace EncorelyApplication.Services;

public class IdentityService : IIdentityService
{
    private readonly IUsuarioQueries _usuarioQueries;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IMusicalProfileQueries _profileQueries;
    private readonly IMusicalProfileRepository _profileRepository;
    private readonly IKafkaProducer<UserSyncEvent> _kafkaProducer;
    private readonly ITokenService _tokenService;
    private readonly ISpotifyService _spotifyService;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        IUsuarioQueries usuarioQueries,
        IUsuarioRepository usuarioRepository,
        IMusicalProfileQueries profileQueries,
        IMusicalProfileRepository profileRepository,
        IKafkaProducer<UserSyncEvent> kafkaProducer,
        ITokenService tokenService,
        ISpotifyService spotifyService,
        ILogger<IdentityService> logger)
    {
        _usuarioQueries = usuarioQueries;
        _usuarioRepository = usuarioRepository;
        _profileQueries = profileQueries;
        _profileRepository = profileRepository;
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
        var googleId = "google_" + Guid.NewGuid().ToString().Substring(0, 8);
        var email = $"{googleId}@google.com";
        
        return await ProcessIdentityAsync(googleId, email, AuthProvider.Google, null, ct);
    }

    public async Task<TokenResponse> RegisterWithEmailAsync(string email, string password, CancellationToken ct = default)
    {
        var existing = await _usuarioQueries.GetByEmailAsync(email);
        if (existing != null)
            throw new Exception("Usuario already exists");

        return await ProcessIdentityAsync(email, email, AuthProvider.Custom, null, ct, password);
    }

    public async Task<TokenResponse> LoginWithEmailAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await _usuarioQueries.GetByEmailAsync(email);
        if (user == null || user.PasswordHash != password) throw new Exception("Invalid credentials");
        
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _usuarioRepository.UpdateAsync(user);

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
        var user = await _usuarioQueries.GetBySpotifyIdAsync(providerId) ?? await _usuarioQueries.GetByEmailAsync(email);

        if (user == null)
        {
            user = new Usuario
            {
                Id = Guid.NewGuid(),
                SpotifyId = providerId,
                DisplayName = displayName ?? email.Split('@')[0],
                Email = email,
                Provider = provider,
                PasswordHash = password,
                CreatedAt = DateTime.UtcNow
            };

            await _usuarioRepository.CreateAsync(user);
            
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

            await _profileRepository.CreateOrUpdateAsync(profile);

            _logger.LogInformation("New user registered via {Provider}: {UserId}", provider, user.Id);
        }

        if (provider == AuthProvider.Spotify && token != null)
        {
            await _kafkaProducer.ProduceAsync(KafkaTopics.UserDnaSync, new UserSyncEvent(user.Id, token, DateTime.UtcNow), ct);
        }

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _usuarioRepository.UpdateAsync(user);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Expiration = DateTime.UtcNow.AddMinutes(30),
            UserId = user.Id
        };
    }
}
