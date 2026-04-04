using EncorelyApplication.DTOs;
using EncorelyApplication.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EncorelyApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IIdentityService _identityService;

    public AuthController(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpPost("spotify")]
    public async Task<IActionResult> SpotifyAuth([FromBody] TokenRequest req, CancellationToken ct)
    {
        var response = await _identityService.LoginWithSpotifyAsync(req.Token, ct);
        return Accepted(response);
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleAuth([FromBody] TokenRequest req, CancellationToken ct)
    {
        var response = await _identityService.LoginWithGoogleAsync(req.Token, ct);
        return Accepted(response);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] EmailAuthRequest req, CancellationToken ct)
    {
        var response = await _identityService.RegisterWithEmailAsync(req.Email, req.Password, ct);
        return Accepted(response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] EmailAuthRequest req, CancellationToken ct)
    {
        var response = await _identityService.LoginWithEmailAsync(req.Email, req.Password, ct);
        return Ok(response);
    }
}

public record TokenRequest(string Token);
public record EmailAuthRequest(string Email, string Password);
