using System.ComponentModel.DataAnnotations;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Auth;

/// <param name="Email">Se normaliza (trim + minúsculas).</param>
/// <param name="Password">12 a 72 bytes UTF-8.</param>
/// <param name="AcceptTerms">Debe ser true: acepta términos y política de tratamiento de datos.</param>
/// <param name="ConsentVersion">Versión aceptada; debe coincidir con GET /api/v1/legal/current.</param>
public sealed record RegisterRequest(
    [Required(ErrorMessage = Msg.Required), EmailAddress(ErrorMessage = Msg.Email), MaxLength(254, ErrorMessage = Msg.MaxLength)] string Email,
    [Required(ErrorMessage = Msg.Required), MinLength(12, ErrorMessage = Msg.PasswordLength), MaxUtf8Bytes(72)] string Password,
    [Required(ErrorMessage = Msg.Required), StringLength(50, MinimumLength = 2, ErrorMessage = Msg.Length)] string DisplayName,
    bool AcceptTerms,
    [Required(ErrorMessage = Msg.Required), MaxLength(32, ErrorMessage = Msg.MaxLength)] string ConsentVersion);

public sealed record LoginRequest(
    [Required(ErrorMessage = Msg.Required), MaxLength(254, ErrorMessage = Msg.MaxLength)] string Email,
    [Required(ErrorMessage = Msg.Required), MaxUtf8Bytes(72)] string Password);

public sealed record RefreshRequest([Required(ErrorMessage = Msg.Required), MaxLength(200, ErrorMessage = Msg.MaxLength)] string RefreshToken);

public sealed record LogoutRequest([Required(ErrorMessage = Msg.Required), MaxLength(200, ErrorMessage = Msg.MaxLength)] string RefreshToken);

public sealed record AuthUserDto(Guid Id, string Email, string DisplayName, bool OnboardingCompleted, UserRole Role);

/// <param name="ExpiresIn">Vida del access token en segundos.</param>
/// <param name="RefreshToken">Opaco, de un solo uso: cada refresh devuelve uno nuevo.</param>
public sealed record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn, AuthUserDto User);

/// <summary>Resultado del flujo PKCE de Spotify en el dispositivo. El back canjea el code: el cliente no lo hace.</summary>
/// <param name="Code">Authorization code recibido en la redirect URI. De un solo uso.</param>
/// <param name="CodeVerifier">El verifier PKCE con el que se generó el challenge (43 a 128 caracteres).</param>
/// <param name="RedirectUri">La misma de la autorización; debe estar en la lista permitida del servidor.</param>
public sealed record SpotifyGrant(
    [Required(ErrorMessage = Msg.Required), MaxLength(2048, ErrorMessage = Msg.MaxLength)] string Code,
    [Required(ErrorMessage = Msg.Required), StringLength(128, MinimumLength = 43, ErrorMessage = Msg.Length)] string CodeVerifier,
    [Required(ErrorMessage = Msg.Required), MaxLength(200, ErrorMessage = Msg.MaxLength)] string RedirectUri);

/// <summary>Entrar con Spotify: el grant PKCE más el consentimiento, que solo cuenta si se crea la cuenta.</summary>
/// <param name="Code">Authorization code recibido en la redirect URI. De un solo uso.</param>
/// <param name="CodeVerifier">El verifier PKCE con el que se generó el challenge (43 a 128 caracteres).</param>
/// <param name="RedirectUri">La misma de la autorización; debe estar en la lista permitida del servidor.</param>
/// <param name="AcceptTerms">Solo se exige si se crea una cuenta nueva; en el login de una cuenta existente se ignora.</param>
/// <param name="ConsentVersion">Versión aceptada; debe coincidir con GET /api/v1/legal/current si se crea la cuenta.</param>
public sealed record SpotifyLoginRequest(
    [Required(ErrorMessage = Msg.Required), MaxLength(2048, ErrorMessage = Msg.MaxLength)] string Code,
    [Required(ErrorMessage = Msg.Required), StringLength(128, MinimumLength = 43, ErrorMessage = Msg.Length)] string CodeVerifier,
    [Required(ErrorMessage = Msg.Required), MaxLength(200, ErrorMessage = Msg.MaxLength)] string RedirectUri,
    bool AcceptTerms,
    [MaxLength(32, ErrorMessage = Msg.MaxLength)] string? ConsentVersion = null)
{
    public SpotifyGrant ToGrant() => new(Code, CodeVerifier, RedirectUri);
}

/// <param name="IsNewUser">true si este login creó la cuenta (sin contraseña, onboarding pendiente).</param>
public sealed record SpotifyAuthResponse(string AccessToken, string RefreshToken, int ExpiresIn, AuthUserDto User, bool IsNewUser);
