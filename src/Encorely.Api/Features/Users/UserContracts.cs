using System.ComponentModel.DataAnnotations;
using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Profiles;
using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Users;

/// <param name="Genres">Slugs de los géneros declarados.</param>
/// <param name="OnboardingCompleted">true cuando hay fecha de nacimiento, ciudad y al menos un género.</param>
/// <param name="AcceptedConsentVersion">Versión más reciente aceptada por la persona; null si no consta ninguna.</param>
/// <param name="ConsentUpToDate">true si <c>AcceptedConsentVersion</c> es la vigente (<c>GET /legal/current</c>); false: pedir aceptar de nuevo.</param>
/// <param name="SpotifyLinked">true si hay una cuenta de Spotify vinculada (sirve para entrar).</param>
/// <param name="HasPassword">false en cuentas creadas con Spotify: no pueden entrar por correo ni desvincular Spotify.</param>
/// <param name="Artists">Mi top de artistas importado de Spotify (máx. 12), como lo ven los demás.</param>
/// <param name="ReactionsReceived">Reacciones a mi perfil, agregadas por objetivo y tipo; nunca dicen quién.</param>
public sealed record MeResponse(
    Guid Id,
    string Email,
    string DisplayName,
    DateOnly? BirthDate,
    int? Age,
    string? City,
    string? Bio,
    IReadOnlyList<string> Genres,
    bool OnboardingCompleted,
    UserRole Role,
    DateTimeOffset CreatedAt,
    string? AcceptedConsentVersion,
    bool ConsentUpToDate,
    bool SpotifyLinked,
    bool HasPassword,
    IReadOnlyList<ProfileArtistDto> Artists,
    ProfileStatsDto Stats,
    IReadOnlyList<ReceivedReactionDto> ReactionsReceived);

/// <summary>PATCH: los campos null no cambian. Bio vacía ("") la borra.</summary>
/// <param name="BirthDate">yyyy-MM-dd; 18 años o más.</param>
/// <param name="Genres">Reemplaza la lista completa de géneros declarados (slugs, 1 a 10).</param>
public sealed record UpdateMeRequest(
    [StringLength(50, MinimumLength = 2, ErrorMessage = Msg.Length)] string? DisplayName = null,
    DateOnly? BirthDate = null,
    [StringLength(80, MinimumLength = 2, ErrorMessage = Msg.Length)] string? City = null,
    [MaxLength(300, ErrorMessage = Msg.MaxLength)] string? Bio = null,
    [MinLength(1, ErrorMessage = "Elige al menos un género."), MaxLength(10, ErrorMessage = Msg.MaxItems), NoNullItems] IReadOnlyList<string>? Genres = null);

/// <summary>
/// Borrar la cuenta reconfirma la identidad: un access token robado no basta para una operación irreversible.
/// Va exactamente uno de los dos: la contraseña, o un grant nuevo de la cuenta de Spotify vinculada.
/// </summary>
public sealed record DeleteMeRequest([MaxUtf8Bytes(72)] string? Password = null, SpotifyGrant? Spotify = null);

public sealed record ConsentRequest([Required(ErrorMessage = Msg.Required), MaxLength(32, ErrorMessage = Msg.MaxLength)] string Version);

/// <summary>Copia de los datos personales (derecho de acceso, Ley 1581). Nunca incluye mensajes: el servidor no los puede leer.</summary>
public sealed record DataExportResponse(
    MeResponse Profile,
    IReadOnlyList<ExportSwipe> Swipes,
    IReadOnlyList<ExportEvent> Events,
    IReadOnlyList<ExportMatch> Matches,
    IReadOnlyList<ExportBlock> Blocks,
    IReadOnlyList<ExportConsent> Consents,
    IReadOnlyList<ExportReaction> Reactions,
    int PushDevices,
    DateTimeOffset GeneratedAt);

public sealed record ExportSwipe(string SongTitle, string Artist, string Direction, DateTimeOffset CreatedAt);
public sealed record ExportEvent(string Name, DateTimeOffset StartsAt);
public sealed record ExportMatch(string PeerDisplayName, string Status, DateTimeOffset CreatedAt, int MessagesSent);
public sealed record ExportBlock(string DisplayName, DateTimeOffset CreatedAt);
public sealed record ExportConsent(string Version, DateTimeOffset AcceptedAt);
/// <summary>Reacción que dejé en el perfil de otra persona.</summary>
public sealed record ExportReaction(string PeerDisplayName, ReactionTargetKind TargetKind, string TargetKey, ReactionType Reaction, DateTimeOffset UpdatedAt);
