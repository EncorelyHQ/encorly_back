using EncorelyApplication.DTOs;
using EncorelyModels;

using EncorelyApplication.DTOs.Auth;

namespace EncorelyApplication.Interfaces;

public interface IIdentityService
{
    Task<TokenResponse> LoginWithSpotifyAsync(string accessToken, CancellationToken ct = default);
    Task<TokenResponse> LoginWithGoogleAsync(string idToken, CancellationToken ct = default);
    Task<TokenResponse> RegisterWithEmailAsync(string email, string password, CancellationToken ct = default);
    Task<TokenResponse> LoginWithEmailAsync(string email, string password, CancellationToken ct = default);
}
