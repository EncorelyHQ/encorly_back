using System.ComponentModel.DataAnnotations;
using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Chat;

/// <summary>Publica (o rota) las llaves PÚBLICAS del usuario. El servidor nunca recibe privadas.</summary>
/// <param name="IdentityKey">Ed25519 pública, 32 B.</param>
/// <param name="BoxKey">X25519 pública, 32 B.</param>
/// <param name="BoxKeyId">Debe ser mayor que el último publicado por el usuario (también tras reinstalar).</param>
/// <param name="BoxKeySig">Firma Ed25519 (64 B) de "ENCORELY-BOXKEY-v1" ‖ boxKeyId (u32 big-endian) ‖ boxKey. La verifica el cliente.</param>
public sealed record UploadKeysRequest(
    [Required(ErrorMessage = Msg.Required), ByteLength(32)] byte[] IdentityKey,
    [Required(ErrorMessage = Msg.Required), ByteLength(32)] byte[] BoxKey,
    [Range(1, int.MaxValue, ErrorMessage = Msg.Range)] int BoxKeyId,
    [Required(ErrorMessage = Msg.Required), ByteLength(64)] byte[] BoxKeySig);

public sealed record KeyBundleDto(Guid UserId, byte[] IdentityKey, byte[] BoxKey, int BoxKeyId, byte[] BoxKeySig, DateTimeOffset UpdatedAt);

public sealed record BoxKeyDto(Guid UserId, int BoxKeyId, byte[] IdentityKey, byte[] BoxKey, byte[] BoxKeySig, DateTimeOffset CreatedAt);

/// <param name="Id">UUID generado por el cliente: idempotencia y parte del AAD.</param>
/// <param name="Version">Versión del formato de cifrado (1).</param>
/// <param name="Nonce">24 B (XChaCha20-Poly1305).</param>
/// <param name="Ciphertext">Incluye el tag de 16 B. Máx. 64 KiB.</param>
/// <param name="FrankingTag">Opcional, 32 B: HMAC-SHA256(frankingKey, "ENCORELY-FRANK-v1" ‖ id (16 B) ‖ texto UTF-8). La frankingKey viaja dentro del ciphertext.</param>
public sealed record SendMessageRequest(
    [NotEmptyGuid] Guid Id,
    [Range(1, 1, ErrorMessage = Msg.Range)] int Version,
    [Range(1, int.MaxValue, ErrorMessage = Msg.Range)] int SenderBoxKeyId,
    [Range(1, int.MaxValue, ErrorMessage = Msg.Range)] int RecipientBoxKeyId,
    [Required(ErrorMessage = Msg.Required), ByteLength(24)] byte[] Nonce,
    [Required(ErrorMessage = Msg.Required), ByteLength(17, 65536)] byte[] Ciphertext,
    [ByteLength(32)] byte[]? FrankingTag = null);

public sealed record MessageAcceptedDto(Guid Id, long Seq, DateTimeOffset CreatedAt);

public sealed record MessageDto(
    Guid Id,
    long Seq,
    Guid SenderId,
    int Version,
    int SenderBoxKeyId,
    int RecipientBoxKeyId,
    byte[] Nonce,
    byte[] Ciphertext,
    byte[]? FrankingTag,
    DateTimeOffset CreatedAt);

/// <param name="NextCursor">Con ?before: seq a pasar como siguiente before. Con ?after: seq a pasar como siguiente after. null si no hay más.</param>
public sealed record MessagePage(IReadOnlyList<MessageDto> Items, long? NextCursor);

/// <summary>Confirma recepción: el servidor borra los mensajes recibidos hasta ese seq (historial local en el dispositivo).</summary>
public sealed record AckRequest([Range(1, long.MaxValue, ErrorMessage = Msg.Range)] long UpToSeq);
