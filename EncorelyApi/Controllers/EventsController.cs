using EncorelyApplication.Interfaces;
using EncorelyInfrastructure.Persistence;
using EncorelyDomain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EncorelyApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly EncorelyDbContext _dbContext;
    private readonly ICompatibilityService _compatibilityService;
    private readonly IUserService _userService;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        IEventService eventService, 
        EncorelyDbContext dbContext, 
        ICompatibilityService compatibilityService,
        IUserService userService,
        ILogger<EventsController> logger)
    {
        _eventService = eventService;
        _dbContext = dbContext;
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
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return NotFound("User not found");

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

        // Tarea 74: Implementation of Advanced Filtering by ConcertMood
        var moodFilter = targetMood ?? user.Mood;
        var candidateUsers = await _dbContext.Users.AsNoTracking()
            .Where(u => u.Id != userId && u.Mood == moodFilter)
            .ToListAsync(ct);

        var compatibleMatches = new List<object>();

        foreach (var candidate in candidateUsers)
        {
            var candidateProfile = await _userService.GetMusicalProfileAsync(candidate.Id, ct);
            if (candidateProfile == null) continue;

            var affinity = _compatibilityService.CalculateAffinity(myProfile, candidateProfile);
            
            // Tarea 75: Priority Match Logic
            var isHighPriority = affinity >= 85.0;

            if (_compatibilityService.IsCompatible(affinity))
            {
                compatibleMatches.Add(new { candidate.Id, candidate.DisplayName, Affinity = affinity, IsHighPriority = isHighPriority, Mood = candidate.Mood });
            }
        }

        // Bubbling high affinity ones up
        compatibleMatches = compatibleMatches.OrderByDescending(m => (double)((dynamic)m).Affinity).ToList();

        return Ok(compatibleMatches);
    }
}
