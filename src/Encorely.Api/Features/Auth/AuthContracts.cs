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
