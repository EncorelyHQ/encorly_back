using System.ComponentModel.DataAnnotations;
using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Profiles;

public sealed record ProfileGenreDto(string Slug, string Name);

/// <param name="Key">Nombre normalizado ("bad-bunny"): identifica al artista al reaccionar y al cruzar perfiles.</param>
/// <param name="ImageUrl">Imagen de Spotify (https://i.scdn.co/…); null si no vino.</param>
/// <param name="Shared">true si el artista también está en el perfil de quien mira (en <c>/me</c> siempre false).</param>
public sealed record ProfileArtistDto(string Key, string Name, string? ImageUrl, bool Shared);

/// <param name="SwipeCount">Canciones calificadas.</param>
/// <param name="GenresExplored">Géneros distintos entre las canciones calificadas.</param>
/// <param name="EventsMarked">Eventos con asistencia marcada (no implica que haya ido).</param>
public sealed record ProfileStatsDto(int SwipeCount, int GenresExplored, int EventsMarked);

/// <param name="Affinity">0–100, o null si alguno de los dos aún no tiene señal suficiente (pocos swipes y sin Spotify).</param>
/// <param name="SharedGenres">Slugs de géneros con peso en ambos (máx. 3).</param>
/// <param name="SharedArtists">Keys de artistas que están en ambos perfiles.</param>
public sealed record ProfileCommonDto(int? Affinity, IReadOnlyList<string> SharedGenres, IReadOnlyList<string> SharedArtists);

public sealed record MyReactionDto(ReactionTargetKind TargetKind, string TargetKey, ReactionType Reaction);

/// <summary>Perfil de otra persona visto por quien llama.</summary>
/// <param name="Genres">Géneros con más peso en su ADN (máx. 8).</param>
/// <param name="Artists">Su top de artistas (máx. 12).</param>
/// <param name="MyReactions">Las reacciones que quien llama dejó en este perfil.</param>
public sealed record PublicProfileDto(
    Guid Id,
    string DisplayName,
    int? Age,
    string? City,
    string? Bio,
    IReadOnlyList<ProfileGenreDto> Genres,
    IReadOnlyList<ProfileArtistDto> Artists,
    ProfileStatsDto Stats,
    ProfileCommonDto Common,
    IReadOnlyList<MyReactionDto> MyReactions);

/// <param name="TargetKey">Slug del género o key del artista; debe estar en el perfil de esa persona.</param>
public sealed record ReactionRequest(
    ReactionTargetKind TargetKind,
    [Required(ErrorMessage = Msg.Required), MaxLength(100, ErrorMessage = Msg.MaxLength)] string TargetKey,
    ReactionType Reaction);

/// <summary>Reacciones recibidas, agregadas: nunca dicen quién.</summary>
/// <param name="TargetName">Nombre del artista o del género, para mostrarlo sin otra consulta.</param>
public sealed record ReceivedReactionDto(ReactionTargetKind TargetKind, string TargetKey, string TargetName, ReactionType Reaction, int Count);
