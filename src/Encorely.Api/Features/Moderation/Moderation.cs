namespace Encorely.Api.Features.Moderation;

public sealed class Block
{
    public Guid BlockerId { get; set; }
    public Guid BlockedId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum ReportReason { Spam, Harassment, Inappropriate, FakeProfile, Underage, Other }

public enum ReportStatus { Open, Resolved, Dismissed }

public sealed class Report
{
    /// <summary>Días tras resolver un reporte en que se borra el texto de la evidencia (igual que el franking que la verifica).</summary>
    public const int EvidenceRetentionDays = 180;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? ReporterId { get; set; }
    public Guid? ReportedUserId { get; set; }
    public Guid? MatchId { get; set; }
    public ReportReason Reason { get; set; }
    public string? Detail { get; set; }
    /// <summary>Mensajes revelados por quien reporta, con el resultado de verificar su franking.</summary>
    public List<ReportEvidence> Evidence { get; set; } = [];
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public string? ResolutionNote { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    /// <summary>Cuándo se borraron Detail y el texto de la evidencia; quedan MessageId y Verified.</summary>
    public DateTimeOffset? EvidencePurgedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ReportEvidence
{
    public Guid MessageId { get; set; }
    public required string Plaintext { get; set; }
    /// <summary>true: el HMAC del texto con la llave revelada coincide con el FrankingTag guardado.</summary>
    public bool Verified { get; set; }
}
