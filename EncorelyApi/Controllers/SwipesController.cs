using EncorelyApplication.Interfaces;
using EncorelyDomain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace EncorelyApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SwipesController : ControllerBase
{
    private readonly ISwipeService _swipeService;

    public SwipesController(ISwipeService swipeService)
    {
        _swipeService = swipeService;
    }

    [HttpGet("next-track")]
    public async Task<IActionResult> GetNextTrack([FromQuery] Guid userId, CancellationToken ct)
    {
        var track = await _swipeService.GetNextTrackAsync(userId, ct);
        return Ok(track);
    }

    [HttpPost("interactions/swipe")]
    public async Task<IActionResult> RegisterSwipe([FromBody] SwipeRequest request, CancellationToken ct)
    {
        await _swipeService.RegisterSwipeAsync(request.UserId, request.TrackId, request.Direction, ct);
        return Accepted();
    }
}

public record SwipeRequest(Guid UserId, string TrackId, SwipeDirection Direction);
