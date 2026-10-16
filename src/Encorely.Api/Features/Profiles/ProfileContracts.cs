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
/// <param name="LikedByMe">Quien llama ya le dio like (en cualquier evento), haya o no match. Igual que en el radar. False en el perfil propio.</param>
/// <param name="Matched">Hay un match activo entre los dos: manda sobre LikedByMe. False en el perfil propio.</param>
/// <param name="CanLike">POST /likes hacia esta persona respondería 200 ahora: van a un mismo evento vigente. False si hay match o en el perfil propio.</param>
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
    IReadOnlyList<MyReactionDto> MyReactions,
    bool LikedByMe,
    bool Matched,
    bool CanLike);

/// <param name="TargetKey">Slug del género o key del artista; debe estar en el perfil de esa persona.</param>
public sealed record ReactionRequest(
    ReactionTargetKind TargetKind,
    [Required(ErrorMessage = Msg.Required), MaxLength(100, ErrorMessage = Msg.MaxLength)] string TargetKey,
    ReactionType Reaction);

/// <summary>Resumen de reacciones recibidas, agregado por objetivo y tipo. El detalle con quién está en <c>/me/reactions/received</c>.</summary>
/// <param name="TargetName">Nombre del artista o del género, para mostrarlo sin otra consulta.</param>
public sealed record ReceivedReactionDto(ReactionTargetKind TargetKind, string TargetKey, string TargetName, ReactionType Reaction, int Count);

public sealed record ReactionFromDto(Guid UserId, string DisplayName);

/// <param name="TargetName">Nombre del artista o género cuando se reaccionó (puede ya no estar en mi perfil).</param>
/// <param name="ReactedAt">Cuándo se puso o se cambió por última vez.</param>
public sealed record ReceivedReactionItemDto(
    ReactionFromDto From, ReactionTargetKind TargetKind, string TargetKey, string TargetName, ReactionType Reaction, DateTimeOffset ReactedAt);

/// <param name="NextCursor">Pasarlo como <c>?before</c> para la página siguiente; null si no hay más.</param>
public sealed record ReceivedReactionPage(IReadOnlyList<ReceivedReactionItemDto> Items, string? NextCursor);
