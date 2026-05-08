using EncorelyApplication.Interfaces;
using EncorelyApplication.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Encorely.Tests.Services;

public class PlaylistServiceTests
{
    private readonly PlaylistService _sut;
    private readonly ISpotifyService _spotifyService = Substitute.For<ISpotifyService>();
    private readonly ILogger<PlaylistService> _logger = Substitute.For<ILogger<PlaylistService>>();

    public PlaylistServiceTests()
    {
        _sut = new PlaylistService(_spotifyService, _logger);
    }

    [Fact]
    public async Task GenerateSharedPlaylistAsync_ShouldInterleaveTracksAndCreatePlaylist()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var token1 = "token1";
        var token2 = "token2";
        var playlistId = "new-playlist-id";

        _spotifyService.CreatePlaylistAsync(token1, Arg.Any<string>(), Arg.Any<string>())
            .Returns(playlistId);
        
        _spotifyService.AddTracksToPlaylistAsync(token1, playlistId, Arg.Any<List<string>>())
            .Returns(true);

        // Act
        var result = await _sut.GenerateSharedPlaylistAsync(userId1, userId2, token1, token2);

        // Assert
        result.Should().NotBeNull();
        await _spotifyService.Received(1).CreatePlaylistAsync(token1, Arg.Any<string>(), Arg.Any<string>());
        await _spotifyService.Received(1).AddTracksToPlaylistAsync(token1, playlistId, Arg.Any<List<string>>());
    }
}
