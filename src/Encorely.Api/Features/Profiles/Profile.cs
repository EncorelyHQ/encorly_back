namespace Encorely.Api.Features.Profiles;

public enum ReactionTargetKind { Artist, Genre }

/// <summary>"Yo también", "Vamos a verlo", "Pásame una".</summary>
public enum ReactionType { MeToo, LetsGo, SendOne }

/// <summary>
/// Reacción de una persona a un artista o género del perfil de otra. Una por (de, para, objetivo): reaccionar de nuevo
/// cambia el tipo. No cuenta para el match ni manda push, y quien la recibe solo ve el conteo, nunca quién.
/// </summary>
public sealed class ProfileReaction
{
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public ReactionTargetKind TargetKind { get; set; }
    /// <summary>Slug del género o <c>ArtistKey</c> del artista.</summary>
    public required string TargetKey { get; set; }
    public ReactionType Reaction { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
