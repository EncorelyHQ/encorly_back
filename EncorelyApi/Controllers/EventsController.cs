using EncorelyApplication.Interfaces;
using EncorelyModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using EncorelyQuery.Interfaces;

namespace EncorelyApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly IUsuarioQueries _usuarioQueries;
    private readonly ICompatibilityService _compatibilityService;
    private readonly IUserService _userService;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        IEventService eventService, 
        IUsuarioQueries usuarioQueries, 
        ICompatibilityService compatibilityService,
        IUserService userService,
        ILogger<EventsController> logger)
    {
        _eventService = eventService;
        _usuarioQueries = usuarioQueries;
        _compatibilityService = compatibilityService;
        _userService = userService;
        _logger = logger;
    }

    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed(CancellationToken ct)
    {
        var events = await _eventService.GetNearbyEventsAsync(ct);
        return Ok(events);
    }

    [HttpGet("{eventId}/matches")]
    public async Task<IActionResult> GetMatches(string eventId, [FromQuery] Guid userId, [FromQuery] ConcertMood? targetMood, CancellationToken ct)
    {
        var user = await _usuarioQueries.GetByIdAsync(userId);
        if (user == null) return NotFound("Usuario not found");

        const int MIN_SWIPES_THRESHOLD = 25;

        if (user.SwipeCount < MIN_SWIPES_THRESHOLD)
        {
            return StatusCode(403, new { message = "Debes completar el Umbral de los 25 swipes" });
        }

        _logger.LogInformation("[UMBRAL] Usuario {UserId} ha alcanzado los 25 swipes. Acceso al Radar CONCEDIDO", userId);

        var myProfile = await _userService.GetMusicalProfileAsync(userId, ct);
        if (myProfile == null)
        {
            return BadRequest(new { message = "Musical profile not found for user." });
        }

        var moodFilter = targetMood ?? user.Mood;
        var candidateUsers = await _usuarioQueries.GetByMoodExceptAsync(moodFilter, userId);

        var compatibleMatches = new List<object>();

        foreach (var candidate in candidateUsers)
        {
            var candidateProfile = await _userService.GetMusicalProfileAsync(candidate.Id, ct);
            if (candidateProfile == null) continue;

            var affinity = _compatibilityService.CalculateAffinity(myProfile, candidateProfile);
            
            var isHighPriority = affinity >= 85.0;

            if (_compatibilityService.IsCompatible(affinity))
            {
                compatibleMatches.Add(new { candidate.Id, candidate.DisplayName, Affinity = affinity, IsHighPriority = isHighPriority, Mood = candidate.Mood });
            }
        }

        compatibleMatches = compatibleMatches.OrderByDescending(m => (double)((dynamic)m).Affinity).ToList();

        return Ok(compatibleMatches);
    }
}
