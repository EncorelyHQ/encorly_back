using EncorelyDomain.Entities;

namespace EncorelyApplication.Interfaces;

public interface ISpotifyService
{
    Task<(string SpotifyId, string Email, string DisplayName)> GetUserProfileAsync(string accessToken, CancellationToken ct = default);
    Task<MusicalProfile> GenerateMusicalProfileAsync(string accessToken, CancellationToken ct = default);
    Task<string> CreatePlaylistAsync(string accessToken, string name, string description, CancellationToken ct = default);
    Task<bool> AddTracksToPlaylistAsync(string accessToken, string playlistId, List<string> trackUris, CancellationToken ct = default);
}
