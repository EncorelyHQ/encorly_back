using EncorelyApplication.DTOs;
using EncorelyDomain.Entities;

namespace EncorelyApplication.Interfaces;

public interface IIdentityService
{
    Task<Guid> LoginWithSpotifyAsync(string accessToken, CancellationToken ct = default);
    Task<Guid> LoginWithGoogleAsync(string idToken, CancellationToken ct = default);
    Task<Guid> RegisterWithEmailAsync(string email, string password, CancellationToken ct = default);
    Task<Guid> LoginWithEmailAsync(string email, string password, CancellationToken ct = default);
}
