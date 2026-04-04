using EncorelyDomain.Entities;
using System.Security.Claims;

namespace EncorelyApplication.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}
