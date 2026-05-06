using EncorelyApplication.Interfaces;
using EncorelyModels;
using SpotifyAPI.Web;

namespace EncorelyApplication.Services;

public class SpotifyService : ISpotifyService
{
    public async Task<(string SpotifyId, string Email, string DisplayName)> GetUserProfileAsync(string accessToken, CancellationToken ct = default)
    {
        var spotify = new SpotifyClient(accessToken);
        var profile = await spotify.UserProfile.Current();
        return (profile.Id, profile.Email, profile.DisplayName);
    }

    public async Task<MusicalProfile> GenerateMusicalProfileAsync(string accessToken, CancellationToken ct = default)
    {
        var spotify = new SpotifyClient(accessToken);
        
        var topTracks = await spotify.Personalization.GetTopTracks(new PersonalizationTopRequest { Limit = 5 });
        
        if (topTracks.Items == null || !topTracks.Items.Any())
            return new MusicalProfile { Energy = 0.5, Danceability = 0.5, Valence = 0.5, Tempo = 120 };

        var trackIds = topTracks.Items.Select(t => t.Id).ToList();
        var featuresResponse = await spotify.Tracks.GetSeveralAudioFeatures(new TracksAudioFeaturesRequest(trackIds));

        var features = featuresResponse.AudioFeatures.Where(f => f != null).ToList();
        
        if (!features.Any())
            return new MusicalProfile { Energy = 0.5, Danceability = 0.5, Valence = 0.5, Tempo = 120 };

        return new MusicalProfile
        {
            Energy = Math.Round(features.Average(f => f.Energy), 2),
            Danceability = Math.Round(features.Average(f => f.Danceability), 2),
            Valence = Math.Round(features.Average(f => f.Valence), 2),
            Tempo = Math.Round(features.Average(f => f.Tempo), 2)
        };
    }

    public async Task<string> CreatePlaylistAsync(string accessToken, string name, string description, CancellationToken ct = default)
    {
        var spotify = new SpotifyClient(accessToken);
        var profile = await spotify.UserProfile.Current();
        var request = new PlaylistCreateRequest(name)
        {
            Public = true,
            Description = description
        };

        var playlist = await spotify.Playlists.Create(profile.Id, request);
        return playlist.Id;
    }

    public async Task<bool> AddTracksToPlaylistAsync(string accessToken, string playlistId, List<string> trackUris, CancellationToken ct = default)
    {
        var spotify = new SpotifyClient(accessToken);
        var request = new PlaylistAddItemsRequest(trackUris);
        var response = await spotify.Playlists.AddItems(playlistId, request);
        return !string.IsNullOrEmpty(response.SnapshotId);
    }
}
