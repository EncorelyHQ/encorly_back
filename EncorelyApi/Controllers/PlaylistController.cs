using EncorelyApplication.DTOs;
using EncorelyApplication.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EncorelyApi.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class PlaylistController : ControllerBase
{
    private readonly IPlaylistService _playlistService;

    public PlaylistController(IPlaylistService playlistService)
    {
        _playlistService = playlistService;
    }

    /// <summary>Tarea 80: Generates a blended DNA Playlist from two matched users' Spotify top tracks.</summary>
    [HttpPost("dna-mix")]
    public async Task<IActionResult> GenerateDnaMix([FromBody] DnaMixRequest request, CancellationToken ct)
    {
        var playlist = await _playlistService.GenerateSharedPlaylistAsync(
            request.UserId1, request.UserId2, request.AccessToken1, request.AccessToken2, ct);
        return Ok(playlist);
    }
}
