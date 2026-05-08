using EncorelyApplication.DTOs;

namespace EncorelyApplication.Interfaces;

public interface IAuthService
{
    Task<Guid> AuthenticateWithSpotifyAsync(SpotifyAuthRequest authRequest, CancellationToken ct = default);
}
