using EncorelyModels;
using System.Security.Claims;

namespace EncorelyApplication.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(Usuario user);
    string GenerateRefreshToken();
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}
