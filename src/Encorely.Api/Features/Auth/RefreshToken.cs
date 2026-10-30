namespace Encorely.Api.Features.Auth;

/// <summary>
/// Refresh token opaco. Solo se guarda su SHA-256. Cada refresh rota el token dentro de la misma familia;
/// presentar uno ya usado revoca la familia completa (detección de robo).
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public required byte[] TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
}
