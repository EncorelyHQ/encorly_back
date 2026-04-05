using EncorelyApplication.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EncorelyApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class VenueController : ControllerBase
{
    private readonly IVenueService _venueService;
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<EncorelyApi.Hubs.VenueHub> _hubContext;

    public VenueController(IVenueService venueService, Microsoft.AspNetCore.SignalR.IHubContext<EncorelyApi.Hubs.VenueHub> hubContext)
    {
        _venueService = venueService;
        _hubContext = hubContext;
    }

    /// <summary>Tarea 78: Creates a temporary Venue Room for a live event.</summary>
    [HttpPost("{eventId}/rooms")]
    public async Task<IActionResult> CreateRoom(string eventId, [FromQuery] string name, [FromQuery] int durationHours = 4, CancellationToken ct = default)
    {
        var room = await _venueService.CreateVenueRoomAsync(eventId, name, TimeSpan.FromHours(durationHours), ct);
        return Ok(new { room.Id, room.Name, room.ExpiresAt, room.EventId });
    }

    /// <summary>Gets active (non-moderated) messages for a venue room.</summary>
    [HttpGet("{roomId}/messages")]
    public async Task<IActionResult> GetMessages(Guid roomId, CancellationToken ct)
    {
        var messages = await _venueService.GetActiveMessagesAsync(roomId, ct);
        return Ok(messages);
    }

    /// <summary>Posts a message to the venue room and runs auto-moderation via keyword scan.</summary>
    [HttpPost("{roomId}/messages")]
    public async Task<IActionResult> PostMessage(Guid roomId, [FromQuery] Guid userId, [FromBody] string content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { message = "El contenido del mensaje no puede estar vacío." });

        var message = await _venueService.PostMessageAsync(roomId, userId, content, ct);

        if (!message.IsModerated)
        {
            await _hubContext.Clients.Group($"venue_{roomId}")
                .SendAsync("ReceiveVenueMessage", userId, content, message.Timestamp);
        }

        return Ok(new { message.Id, message.Content, message.IsModerated, message.Timestamp });
    }

    /// <summary>Tarea 79: Admin endpoint to manually moderate a message.</summary>
    [HttpDelete("messages/{messageId}")]
    public async Task<IActionResult> ModerateMessage(Guid messageId, [FromQuery] string reason = "Moderación manual", CancellationToken ct = default)
    {
        var removed = await _venueService.ModerateMessageAsync(messageId, reason, ct);
        if (!removed) return NotFound(new { message = "Mensaje no encontrado." });
        return Ok(new { message = "Mensaje moderado exitosamente." });
    }
}
