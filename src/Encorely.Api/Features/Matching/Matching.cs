namespace Encorely.Api.Features.Matching;

/// <summary>Like de persona a persona. El match nace solo si existe el like recíproco.</summary>
public sealed class UserLike
{
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public Guid? EventId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum MatchStatus { Active, Closed }

public enum MatchCloseReason { Unmatched, Blocked, AccountDeleted, Banned }

public sealed class Match
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Par ordenado: UserAId &lt; UserBId (comparación de Guid). Un solo match activo por par.</summary>
    public Guid UserAId { get; set; }
    public Guid UserBId { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Active;
    public Guid? EventId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public MatchCloseReason? CloseReason { get; set; }
    /// <summary>Último número de secuencia asignado a un mensaje del match.</summary>
    public long LastSeq { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }

    public bool Involves(Guid userId) => UserAId == userId || UserBId == userId;
    public Guid PeerOf(Guid userId) => UserAId == userId ? UserBId : UserAId;

    public static (Guid A, Guid B) OrderPair(Guid x, Guid y) => x.CompareTo(y) < 0 ? (x, y) : (y, x);
}
