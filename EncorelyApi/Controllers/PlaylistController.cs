using EncorelyApplication.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EncorelyApi.Controllers;

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
    public async Task<IActionResult> GenerateDnaMix(
        [FromQuery] Guid userId1,
        [FromQuery] Guid userId2,
        [FromQuery] string accessToken1,
        [FromQuery] string accessToken2,
        CancellationToken ct)
    {
        var playlist = await _playlistService.GenerateSharedPlaylistAsync(userId1, userId2, accessToken1, accessToken2, ct);
        return Ok(playlist);
    }
}
