using System.Security.Claims;
using Encorely.Api.Infrastructure.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Encorely.Api.Infrastructure.Auth;

public static class CurrentUser
{
    /// <summary>La ÚNICA fuente de identidad: el claim <c>sub</c> del JWT validado. Nunca query/body.</summary>
    public static Guid UserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id)
            ? id
            : throw ApiException.Unauthorized(ErrorCodes.Unauthorized, "Necesitas iniciar sesión.");
    }

    /// <summary>Sesión (familia de refresh) del access token; null en tokens emitidos antes de existir el claim.</summary>
    public static Guid? SessionId(this ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(TokenService.SessionClaim), out var id) ? id : null;

    public static Guid? TryUserId(this ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}
