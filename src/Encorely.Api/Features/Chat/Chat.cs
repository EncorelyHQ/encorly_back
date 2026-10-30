namespace Encorely.Api.Features.Chat;

/// <summary>Estado de llaves vigente del usuario (una identidad por usuario en v1).</summary>
public sealed class UserKey
{
    public Guid UserId { get; set; }
    /// <summary>Ed25519 pública (32 B).</summary>
    public required byte[] IdentityKey { get; set; }
    public int CurrentBoxKeyId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Historial de llaves de caja X25519 públicas, firmadas por la identidad. Nunca se guardan privadas.</summary>
public sealed class UserBoxKey
{
    public Guid UserId { get; set; }
    public int BoxKeyId { get; set; }
    public required byte[] IdentityKey { get; set; }
    public required byte[] BoxKey { get; set; }
    public required byte[] Signature { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Mensaje cifrado de extremo a extremo. El servidor solo ve ciphertext, nonce y metadatos;
/// nunca texto plano. El Id lo genera el cliente (idempotencia y parte del AAD).
/// </summary>
public sealed class ChatMessage
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid SenderId { get; set; }
    public long Seq { get; set; }
    public short Version { get; set; }
    public int SenderBoxKeyId { get; set; }
    public int RecipientBoxKeyId { get; set; }
    public required byte[] Nonce { get; set; }
    public required byte[] Ciphertext { get; set; }
    /// <summary>Compromiso de franking (HMAC-SHA256) para reportes verificables. Opcional.</summary>
    public byte[]? FrankingTag { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Copia del compromiso de franking de un mensaje, con vida propia: el ack del receptor y la retención borran el
/// mensaje, pero el reporte debe poder verificarse después. Solo guarda el tag (nunca ciphertext) y vence a los 180 días.
/// </summary>
public sealed class MessageFrankingRecord
{
    public const int RetentionDays = 180;

    public Guid MessageId { get; set; }
    public Guid MatchId { get; set; }
    public Guid SenderId { get; set; }
    public Guid RecipientId { get; set; }
    public required byte[] FrankingTag { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Lápida del id de un mensaje sin franking que el receptor confirmó: el ack borra el mensaje y, sin esta fila, el id
/// quedaría libre para un duplicado con seq nuevo. Los mensajes con tag ya quedan reservados por su <see cref="MessageFrankingRecord"/>.
/// </summary>
public sealed class DeliveredMessageId
{
    public Guid MessageId { get; set; }
    public Guid MatchId { get; set; }
    public Guid SenderId { get; set; }
    public DateTimeOffset DeliveredAt { get; set; }
}
