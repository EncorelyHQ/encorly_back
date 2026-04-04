using EncorelyApplication.Interfaces;
using EncorelyDomain.Entities;
using EncorelyDomain.Events;
using Microsoft.EntityFrameworkCore;

namespace EncorelyApplication.Services;

public class VenueService : IVenueService
{
    private readonly IEncorelyDbContext _dbContext;
    private readonly IEventProducer<VenueMessageFlaggedEvent> _moderationProducer;

    // Moderation keywords — Tarea 79
    private static readonly HashSet<string> BannedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "spam", "odio", "hate", "violencia", "violence", "scam"
    };

    public VenueService(IEncorelyDbContext dbContext, IEventProducer<VenueMessageFlaggedEvent> moderationProducer)
    {
        _dbContext = dbContext;
        _moderationProducer = moderationProducer;
    }

    public async Task<VenueRoom> CreateVenueRoomAsync(string eventId, string name, TimeSpan duration, CancellationToken ct = default)
    {
        var room = new VenueRoom
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Name = name,
            ExpiresAt = DateTime.UtcNow.Add(duration)
        };

        await _dbContext.VenueRooms.AddAsync(room, ct);
        await _dbContext.SaveChangesAsync(ct);
        return room;
    }

    public async Task<IEnumerable<VenueMessage>> GetActiveMessagesAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _dbContext.VenueMessages
            .Where(m => m.RoomId == roomId && !m.IsModerated)
            .OrderBy(m => m.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<VenueMessage> PostMessageAsync(Guid roomId, Guid senderId, string content, CancellationToken ct = default)
    {
        var room = await _dbContext.VenueRooms.FindAsync(new object[] { roomId }, ct);
        if (room == null || !room.IsActive)
            throw new InvalidOperationException("La sala de venue no está activa o ha expirado.");

        // Tarea 79: Auto-moderation via keyword scanning
        var isFlagged = BannedKeywords.Any(kw => content.Contains(kw, StringComparison.OrdinalIgnoreCase));

        var message = new VenueMessage
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            SenderId = senderId,
            Content = content,
            IsModerated = isFlagged,
            Timestamp = DateTime.UtcNow
        };

        await _dbContext.VenueMessages.AddAsync(message, ct);
        await _dbContext.SaveChangesAsync(ct);

        if (isFlagged)
        {
            var flagEvent = new VenueMessageFlaggedEvent(roomId, message.Id, senderId, "Keyword banned", DateTime.UtcNow);
            await _moderationProducer.ProduceAsync(KafkaTopics.VenueModerationFlagged, flagEvent, ct);
        }

        return message;
    }

    public async Task<bool> ModerateMessageAsync(Guid messageId, string reason, CancellationToken ct = default)
    {
        var message = await _dbContext.VenueMessages.FindAsync(new object[] { messageId }, ct);
        if (message == null) return false;

        message.IsModerated = true;
        await _dbContext.SaveChangesAsync(ct);

        var flagEvent = new VenueMessageFlaggedEvent(message.RoomId, messageId, message.SenderId, reason, DateTime.UtcNow);
        await _moderationProducer.ProduceAsync(KafkaTopics.VenueModerationFlagged, flagEvent, ct);

        return true;
    }
}
