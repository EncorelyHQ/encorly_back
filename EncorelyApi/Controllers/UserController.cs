using EncorelyApplication.Interfaces;
using EncorelyDomain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace EncorelyApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe([FromQuery] Guid userId, CancellationToken ct)
    {
        var user = await _userService.GetMeAsync(userId, ct);
        return Ok(new
        {
            user.Id,
            user.DisplayName,
            user.Email,
            Provider = user.Provider.ToString(),
            user.SwipeCount,
            Mood = user.Mood.ToString()
        });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request, CancellationToken ct)
    {
        await _userService.UpdateSettingsAsync(request.UserId, request.Mood, ct);
        return NoContent();
    }
}

public record UpdateSettingsRequest(Guid UserId, ConcertMood Mood);
