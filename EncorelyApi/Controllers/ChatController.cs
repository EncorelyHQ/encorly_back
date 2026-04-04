using EncorelyApplication.Interfaces;
using EncorelyDomain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace EncorelyApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IMatchService _matchService;
    private readonly IUserService _userService;

    public ChatController(IMatchService matchService, IUserService userService)
    {
        _matchService = matchService;
        _userService = userService;
    }

    [HttpGet("{roomId}/messages")]
    public async Task<IActionResult> GetMessages(Guid roomId, [FromQuery] Guid userId, CancellationToken ct)
    {
        var user = await _userService.GetMeAsync(userId, ct);

        if (user.Provider == AuthProvider.Google && user.SwipeCount < 25)
        {
            return BadRequest(new { message = "Su ADN musical aún no es suficiente para chatear" });
        }

        var messages = await _matchService.GetChatMessagesAsync(roomId, ct);
        return Ok(messages);
    }
}
