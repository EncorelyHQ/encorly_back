using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Encorely.Api.Infrastructure.Auth;

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public const string SessionClaim = "sid";

    private readonly JwtOptions _jwt = options.Value;

    public static SymmetricSecurityKey SigningKey(JwtOptions jwt) => new(Encoding.UTF8.GetBytes(jwt.SigningKey));

    /// <param name="sessionId">Familia del refresh token: el claim <c>sid</c> liga el access token (y sus push tokens) a una sesión.</param>
    public (string Token, int ExpiresInSeconds) CreateAccessToken(User user, Guid sessionId)
    {
        var now = clock.GetUtcNow();
        var lifetime = TimeSpan.FromMinutes(_jwt.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("role", user.Role.ToString().ToLowerInvariant()),
            new(SessionClaim, sessionId.ToString()),
        };
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = (now + lifetime).UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(SigningKey(_jwt), SecurityAlgorithms.HmacSha256),
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), (int)lifetime.TotalSeconds);
    }

    /// <summary>Token opaco de 256 bits para el cliente + su SHA-256 para guardar.</summary>
    public static (string Token, byte[] Hash) CreateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (token, HashRefreshToken(token));
    }

    public static byte[] HashRefreshToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
