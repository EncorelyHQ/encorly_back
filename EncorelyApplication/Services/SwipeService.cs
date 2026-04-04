using EncorelyApplication.Interfaces;
using EncorelyDomain.Entities;
using EncorelyDomain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EncorelyApplication.Services;

public class SwipeService : ISwipeService
{
    private const int MIN_SWIPES_THRESHOLD = 25; // Adjusted from 100 to 25 (Tarea 3)
    
    private readonly IEncorelyDbContext _dbContext;
    private readonly IKafkaProducer<SwipeRegisteredEvent> _kafkaProducer;
    private readonly ILogger<SwipeService> _logger;

    public SwipeService(
        IEncorelyDbContext dbContext,
        IKafkaProducer<SwipeRegisteredEvent> kafkaProducer,
        ILogger<SwipeService> logger)
    {
        _dbContext = dbContext;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    public async Task RegisterSwipeAsync(Guid userId, string trackId, SwipeDirection direction, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, ct);
        if (user == null) throw new KeyNotFoundException("User not found");

        var swipe = new Swipe { Id = Guid.NewGuid(), UserId = userId, TrackId = trackId, Direction = direction };
        await _dbContext.Swipes.AddAsync(swipe, ct);
        
        user.SwipeCount++;
        await _dbContext.SaveChangesAsync(ct);

        await _kafkaProducer.ProduceAsync(KafkaTopics.SwipeRawEvents, new SwipeRegisteredEvent(userId, trackId, direction.ToString()), ct);
    }

    public async Task<object> GetNextTrackAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        
        if (user != null && user.Provider != AuthProvider.Spotify)
        {
            _logger.LogInformation("Non-Spotify user {UserId} using Musical Bridge (Client Credentials)", userId);
            return new { SpotifyId = "pop_track_456", Name = "Popular Hit", Artist = "Encorely Artist", PreviewUrl = "https://p.scdn.co/mp3-preview/bridge" };
        }

        return new { SpotifyId = "5upAn8J6pXW4G4lWf3P4", Name = "Vampire Empire", Artist = "Big Thief", PreviewUrl = "https://p.scdn.co/mp3-preview/direct" };
    }
}
