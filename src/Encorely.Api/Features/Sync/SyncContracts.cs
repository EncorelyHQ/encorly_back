using System.ComponentModel.DataAnnotations;
using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Sync;

/// <param name="Algorithm">argon2id (recomendado) | scrypt | pbkdf2-sha256.</param>
public sealed record KdfParams(
    [Required(ErrorMessage = Msg.Required), MaxLength(20, ErrorMessage = Msg.MaxLength)] string Algorithm,
    [Range(1, uint.MaxValue, ErrorMessage = Msg.Range)] long OpsLimit,
    [Range(1, SyncService.MaxKdfMemLimit, ErrorMessage = Msg.Range)] long MemLimit);

/// <param name="Ciphertext">Máx. 10 MiB. Cifrado en el dispositivo con una llave derivada de la passphrase.</param>
/// <param name="Nonce">24 B.</param>
/// <param name="Salt">16 a 64 B.</param>
public sealed record PutBackupRequest(
    [Range(1, 1000, ErrorMessage = Msg.Range)] int Version,
    [Required(ErrorMessage = Msg.Required), ByteLength(16, 10 * 1024 * 1024)] byte[] Ciphertext,
    [Required(ErrorMessage = Msg.Required), ByteLength(24)] byte[] Nonce,
    [Required(ErrorMessage = Msg.Required), ByteLength(16, 64)] byte[] Salt,
    [Required(ErrorMessage = Msg.Required)] KdfParams Kdf);

public sealed record BackupInfoDto(int Version, long SizeBytes, DateTimeOffset UpdatedAt);

public sealed record BackupDto(int Version, byte[] Ciphertext, byte[] Nonce, byte[] Salt, KdfParams Kdf, DateTimeOffset UpdatedAt);

/// <param name="EphemeralKey">X25519 pública efímera del dispositivo NUEVO (32 B). Va también en el QR para que el dispositivo viejo la verifique.</param>
public sealed record CreateLinkRequest([Required(ErrorMessage = Msg.Required), ByteLength(32)] byte[] EphemeralKey);

/// <param name="Ciphertext">Material de llaves cifrado hacia la llave efímera del nuevo. Máx. 64 KiB.</param>
public sealed record FulfillLinkRequest(
    [Required(ErrorMessage = Msg.Required), ByteLength(32)] byte[] SenderEphemeralKey,
    [Required(ErrorMessage = Msg.Required), ByteLength(24)] byte[] Nonce,
    [Required(ErrorMessage = Msg.Required), ByteLength(17, 65536)] byte[] Ciphertext);

public sealed record LinkRequestDto(
    Guid Id,
    LinkRequestStatus Status,
    byte[] NewDeviceEphemeralKey,
    byte[]? SenderEphemeralKey,
    byte[]? Nonce,
    byte[]? Ciphertext,
    DateTimeOffset ExpiresAt);
