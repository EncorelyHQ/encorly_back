using System.ComponentModel.DataAnnotations;
using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Legal;

public sealed record LegalResponse(string ConsentVersion, DateOnly EffectiveDate, string TermsUrl, string PrivacyUrl, string AccountDeletionUrl, string? PrivacyContactEmail);

/// <summary>Borrado de cuenta desde la web (requisito de Google Play), sin la app.</summary>
public sealed record AccountDeletionRequest(
    [Required(ErrorMessage = Msg.Required), MaxLength(254, ErrorMessage = Msg.MaxLength)] string Email,
    [Required(ErrorMessage = Msg.Required), MaxUtf8Bytes(72)] string Password);
