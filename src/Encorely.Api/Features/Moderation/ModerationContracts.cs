using System.ComponentModel.DataAnnotations;
using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Moderation;

public sealed record BlockRequest([NotEmptyGuid] Guid UserId);

public sealed record BlockedUserDto(Guid UserId, string DisplayName, DateTimeOffset CreatedAt);

/// <param name="Evidence">Mensajes que quien reporta decide revelar (máx. 20). Se verifican con franking si el mensaje lo trae.</param>
public sealed record ReportRequest(
    [NotEmptyGuid] Guid UserId,
    ReportReason Reason,
    [MaxLength(1000, ErrorMessage = Msg.MaxLength)] string? Detail = null,
    Guid? MatchId = null,
    [MaxLength(20, ErrorMessage = Msg.MaxItems)] IReadOnlyList<ReportEvidenceRequest>? Evidence = null);

/// <param name="FrankingKey">32 B, la llave que venía dentro del mensaje descifrado.</param>
public sealed record ReportEvidenceRequest(
    [NotEmptyGuid] Guid MessageId,
    [Required(ErrorMessage = Msg.Required), MaxLength(8000, ErrorMessage = Msg.MaxLength)] string Plaintext,
    [Required(ErrorMessage = Msg.Required), ByteLength(32)] byte[] FrankingKey);

public sealed record ReportCreatedDto(Guid Id);
