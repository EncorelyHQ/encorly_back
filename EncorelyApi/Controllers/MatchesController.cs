using EncorelyApplication.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EncorelyApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly IMatchService _matchService;

    public MatchesController(IMatchService matchService)
    {
        _matchService = matchService;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending([FromQuery] Guid userId, CancellationToken ct)
    {
        var pending = await _matchService.GetPendingMatchesAsync(userId, ct);
        return Ok(pending);
    }

    [HttpPost("{matchId}/accept")]
    public async Task<IActionResult> AcceptMatch(Guid matchId, [FromQuery] Guid userId, CancellationToken ct)
    {
        var roomId = await _matchService.AcceptMatchAsync(userId, matchId, ct);
        return Ok(new { roomId });
    }
}
