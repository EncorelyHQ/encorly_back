using EncorelyApplication.Interfaces;
using EncorelyModels;
using EncorelyDomain.Events;
using EncorelyQuery.Interfaces;
using EncorelyRepository.Interfaces;

namespace EncorelyApplication.Services;

public class VenueService : IVenueService
{
    private readonly IVenueRoomQueries _roomQueries;
    private readonly IVenueRoomRepository _roomRepository;
    private readonly IVenueMessageQueries _messageQueries;
    private readonly IVenueMessageRepository _messageRepository;
    private readonly IEventProducer<VenueMessageFlaggedEvent> _moderationProducer;

    private static readonly HashSet<string> BannedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "spam", "odio", "hate", "violencia", "violence", "scam"
    };

    public VenueService(
        IVenueRoomQueries roomQueries,
        IVenueRoomRepository roomRepository,
        IVenueMessageQueries messageQueries,
        IVenueMessageRepository messageRepository,
        IEventProducer<VenueMessageFlaggedEvent> moderationProducer)
    {
        _roomQueries = roomQueries;
        _roomRepository = roomRepository;
        _messageQueries = messageQueries;
        _messageRepository = messageRepository;
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

        await _roomRepository.CreateAsync(room);
        return room;
    }

    public async Task<IEnumerable<VenueMessage>> GetActiveMessagesAsync(Guid roomId, CancellationToken ct = default)
    {
        var messages = await _messageQueries.GetByRoomIdAsync(roomId);
        return messages.Where(m => !m.IsModerated);
    }

    public async Task<VenueMessage> PostMessageAsync(Guid roomId, Guid senderId, string content, CancellationToken ct = default)
    {
        var room = await _roomQueries.GetByIdAsync(roomId);
        if (room == null || !room.IsActive)
            throw new InvalidOperationException("La sala de venue no está activa o ha expirado.");

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

        await _messageRepository.CreateAsync(message);

        if (isFlagged)
        {
            var flagEvent = new VenueMessageFlaggedEvent(roomId, message.Id, senderId, "Keyword banned", DateTime.UtcNow);
            await _moderationProducer.ProduceAsync(KafkaTopics.VenueModerationFlagged, flagEvent, ct);
        }

        return message;
    }

    public async Task<bool> ModerateMessageAsync(Guid messageId, string reason, CancellationToken ct = default)
    {
        var message = await _messageQueries.GetByIdAsync(messageId);
        if (message == null) return false;

        message.IsModerated = true;
        await _messageRepository.UpdateAsync(message);

        var flagEvent = new VenueMessageFlaggedEvent(message.RoomId, messageId, message.SenderId, reason, DateTime.UtcNow);
        await _moderationProducer.ProduceAsync(KafkaTopics.VenueModerationFlagged, flagEvent, ct);

        return true;
    }
}
