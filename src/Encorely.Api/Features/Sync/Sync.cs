namespace Encorely.Api.Features.Sync;

/// <summary>
/// Respaldo del historial y las llaves, cifrado en el dispositivo con una passphrase que el servidor nunca ve.
/// Para el servidor es un blob opaco: uno por usuario, se reemplaza completo.
/// </summary>
public sealed class EncryptedBackup
{
    public Guid UserId { get; set; }
    public int Version { get; set; }
    public required byte[] Ciphertext { get; set; }
    public required byte[] Nonce { get; set; }
    public required byte[] Salt { get; set; }
    public required string KdfAlgorithm { get; set; }
    public long KdfOpsLimit { get; set; }
    public long KdfMemLimit { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum LinkRequestStatus { Pending, Ready }

/// <summary>
/// Buzón de un solo uso para vincular un dispositivo nuevo: el nuevo publica una llave efímera X25519, el que ya
/// tiene las llaves le deja el material cifrado hacia esa llave, y el nuevo lo recoge. El servidor solo ve ciphertext.
/// </summary>
public sealed class DeviceLinkRequest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public required byte[] NewDeviceEphemeralKey { get; set; }
    public LinkRequestStatus Status { get; set; } = LinkRequestStatus.Pending;
    public byte[]? SenderEphemeralKey { get; set; }
    public byte[]? Nonce { get; set; }
    public byte[]? Ciphertext { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
}
