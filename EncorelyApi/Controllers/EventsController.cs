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
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        IEventService eventService, 
        EncorelyDbContext dbContext, 
        ICompatibilityService compatibilityService,
        ILogger<EventsController> logger)
    {
        _eventService = eventService;
        _dbContext = dbContext;
        _compatibilityService = compatibilityService;
        _logger = logger;
    }

    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed(CancellationToken ct)
    {
        var events = await _eventService.GetNearbyEventsAsync(ct);
        return Ok(events);
    }

    [HttpGet("{eventId}/matches")]
    public async Task<IActionResult> GetMatches(string eventId, [FromQuery] Guid userId, CancellationToken ct)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return NotFound("User not found");

        const int MIN_SWIPES_THRESHOLD = 25;

        if (user.SwipeCount < MIN_SWIPES_THRESHOLD)
        {
            return StatusCode(403, new { message = "Debes completar el Umbral de los 25 swipes" });
        }

        _logger.LogInformation("[UMBRAL] Usuario {UserId} ha alcanzado los 25 swipes. Acceso al Radar CONCEDIDO", userId);

        var otherUsers = new List<(Guid Id, string Name, double Energy, double Danceability)>
        {
            (Guid.NewGuid(), "Moshpit King", 0.9, 0.8),
            (Guid.NewGuid(), "Chill Listener", 0.2, 0.3),
            (Guid.NewGuid(), "Dance Machine", 0.8, 0.95)
        };

        var myProfile = new MusicalProfile { Energy = 0.85, Danceability = 0.80, Valence = 0.70 };
        
        var compatibleMatches = otherUsers
            .Select(u => new { u.Id, DisplayName = u.Name, Affinity = _compatibilityService.CalculateAffinity(myProfile, new MusicalProfile { Energy = u.Energy, Danceability = u.Danceability, Valence = 0.6 }) })
            .Where(m => _compatibilityService.IsCompatible(m.Affinity))
            .ToList();

        return Ok(compatibleMatches);
    }
}
