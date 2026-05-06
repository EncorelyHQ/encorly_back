using EncorelyQuery.Interfaces;
using EncorelyRepository.Interfaces;
using EncorelyModels;
using Microsoft.AspNetCore.Mvc;

namespace EncorelyApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUsuarioQueries _usuarioQueries;
    private readonly IUsuarioRepository _usuarioRepository;

    public UserController(IUsuarioQueries usuarioQueries, IUsuarioRepository usuarioRepository)
    {
        _usuarioQueries = usuarioQueries;
        _usuarioRepository = usuarioRepository;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe([FromQuery] Guid userId, CancellationToken ct)
    {
        var user = await _usuarioQueries.GetByIdAsync(userId);
        if (user == null) return NotFound();

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
        var user = await _usuarioQueries.GetByIdAsync(request.UserId);
        if (user == null) return NotFound();

        user.Mood = request.Mood;
        await _usuarioRepository.UpdateAsync(user);
        
        return NoContent();
    }
}

public record UpdateSettingsRequest(Guid UserId, ConcertMood Mood);
