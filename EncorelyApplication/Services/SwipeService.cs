using EncorelyApplication.Interfaces;
using EncorelyModels;
using EncorelyDomain.Events;
using Microsoft.Extensions.Logging;
using EncorelyQuery.Interfaces;
using EncorelyRepository.Interfaces;

namespace EncorelyApplication.Services;

public class SwipeService : ISwipeService
{
    private const int MIN_SWIPES_THRESHOLD = 25; 
    
    private readonly IUsuarioQueries _usuarioQueries;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ISwipeRepository _swipeRepository;
    private readonly IKafkaProducer<SwipeRegisteredEvent> _kafkaProducer;
    private readonly ILogger<SwipeService> _logger;

    public SwipeService(
        IUsuarioQueries usuarioQueries,
        IUsuarioRepository usuarioRepository,
        ISwipeRepository swipeRepository,
        IKafkaProducer<SwipeRegisteredEvent> kafkaProducer,
        ILogger<SwipeService> logger)
    {
        _usuarioQueries = usuarioQueries;
        _usuarioRepository = usuarioRepository;
        _swipeRepository = swipeRepository;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    public async Task RegisterSwipeAsync(Guid userId, string trackId, SwipeDirection direction, CancellationToken ct = default)
    {
        var user = await _usuarioQueries.GetByIdAsync(userId);
        if (user == null) throw new KeyNotFoundException("Usuario not found");

        var swipe = new Swipe { Id = Guid.NewGuid(), UserId = userId, TrackId = trackId, Direction = direction };
        await _swipeRepository.CreateAsync(swipe);
        
        user.SwipeCount++;
        await _usuarioRepository.UpdateAsync(user);

        await _kafkaProducer.ProduceAsync(KafkaTopics.SwipeRawEvents, new SwipeRegisteredEvent(userId, trackId, direction.ToString()), ct);
    }

    public async Task<object> GetNextTrackAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _usuarioQueries.GetByIdAsync(userId);
        
        if (user != null && user.Provider != AuthProvider.Spotify)
        {
            _logger.LogInformation("Non-Spotify user {UserId} using Musical Bridge (Client Credentials)", userId);
            return new { SpotifyId = "pop_track_456", Name = "Popular Hit", Artist = "Encorely Artist", PreviewUrl = "https://p.scdn.co/mp3-preview/bridge" };
        }

        return new { SpotifyId = "5upAn8J6pXW4G4lWf3P4", Name = "Vampire Empire", Artist = "Big Thief", PreviewUrl = "https://p.scdn.co/mp3-preview/direct" };
    }
}
