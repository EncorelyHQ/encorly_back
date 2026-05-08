using EncorelyApplication.Interfaces;
using SpotifyAPI.Web;
using Microsoft.Extensions.Logging;

namespace EncorelyApplication.Services;

/// <summary>Tarea 80: Generates a shared DNA Playlist by blending top tracks from two matched users.</summary>
public class PlaylistService : IPlaylistService
{
    private readonly ISpotifyService _spotifyService;
    private readonly ILogger<PlaylistService> _logger;

    public PlaylistService(ISpotifyService spotifyService, ILogger<PlaylistService> logger)
    {
        _spotifyService = spotifyService;
        _logger = logger;
    }

    public async Task<object> GenerateSharedPlaylistAsync(Guid userId1, Guid userId2, string accessToken1, string accessToken2, CancellationToken ct = default)
    {
        // Fetch top tracks for both users using their individual Spotify access tokens
        var tracksUser1 = await GetTopTrackIdsAsync(accessToken1, ct);
        var tracksUser2 = await GetTopTrackIdsAsync(accessToken2, ct);

        // DNA Blend: Interleave both lists to create a shared playlist
        var blendedIds = InterleaveTrackLists(tracksUser1, tracksUser2);
        var trackUris = blendedIds.Select(id => id.StartsWith("spotify:track:") ? id : $"spotify:track:{id}").ToList();

        // Tarea 80: Create real playlist on Spotify (using Usuario 1 account as host)
        var playlistName = $"Encorely DNA Mix 🎵";
        var playlistDesc = $"Mezcla musical generada por afinidad entre usuarios en Encorely.";
        
        var playlistId = await _spotifyService.CreatePlaylistAsync(accessToken1, playlistName, playlistDesc, ct);
        var success = await _spotifyService.AddTracksToPlaylistAsync(accessToken1, playlistId, trackUris, ct);

        _logger.LogInformation("[DNA Playlist] Real playlist created {Id} for users {U1} & {U2}", playlistId, userId1, userId2);

        return new
        {
            SpotifyPlaylistId = playlistId,
            Name = playlistName,
            Description = playlistDesc,
            TotalTracks = trackUris.Count,
            ExternalUrl = $"https://open.spotify.com/playlist/{playlistId}",
            SyncSuccess = success
        };
    }

    private async Task<List<string>> GetTopTrackIdsAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            var spotify = new SpotifyClient(accessToken);
            var topTracks = await spotify.Personalization.GetTopTracks(new PersonalizationTopRequest
            {
                Limit = 10,
                TimeRangeParam = PersonalizationTopRequest.TimeRange.ShortTerm
            });

            return topTracks.Items?.Select(t => t.Id).ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch Spotify top tracks, using mock fallback.");
            return new List<string> { "spotify:track:mock1", "spotify:track:mock2", "spotify:track:mock3" };
        }
    }

    private static List<string> InterleaveTrackLists(List<string> list1, List<string> list2)
    {
        var result = new List<string>();
        var max = Math.Max(list1.Count, list2.Count);

        for (int i = 0; i < max; i++)
        {
            if (i < list1.Count && !result.Contains(list1[i])) result.Add(list1[i]);
            if (i < list2.Count && !result.Contains(list2[i])) result.Add(list2[i]);
        }

        return result;
    }
}
