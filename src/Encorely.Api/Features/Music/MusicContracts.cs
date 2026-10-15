using System.ComponentModel.DataAnnotations;
using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Music;

public sealed record GenreDto(int Id, string Slug, string Name);

/// <param name="Genre">Slug del género.</param>
/// <param name="PreviewUrl">Preview de 30 s (iTunes). null si no hay.</param>
public sealed record SongDto(Guid Id, string Title, string Artist, string Genre, string GenreName, string CoverUrl, string? PreviewUrl);

public sealed record SwipeRequest([NotEmptyGuid] Guid SongId, SwipeDirection Direction);

/// <param name="Counted">false si esa canción ya estaba calificada (repetir no suma).</param>
public sealed record SwipeResponse(bool Counted, int SwipeCount);

/// <param name="Weight">Peso normalizado 0–1; los pesos suman 1 (vacío si aún no hay señal).</param>
public sealed record DnaGenreDto(string Genre, string Name, double Weight);

/// <param name="RadarThreshold">Swipes mínimos para usar el radar (configurable en el servidor).</param>
public sealed record DnaResponse(IReadOnlyList<DnaGenreDto> Genres, int SwipeCount, int RadarThreshold, bool RadarUnlocked);

/// <summary>Gustos importados de Spotify por el cliente (PKCE en el dispositivo; el token nunca llega al servidor).</summary>
public sealed record SpotifyImportRequest(
    [Required(ErrorMessage = Msg.Required), MaxLength(50, ErrorMessage = Msg.MaxItems), NoNullItems] IReadOnlyList<SpotifyArtist> TopArtists);

/// <param name="ImageUrl">Opcional: <c>images[0].url</c> de Spotify. Solo se guarda si es https de i.scdn.co; si no, se ignora.</param>
public sealed record SpotifyArtist(
    [Required(ErrorMessage = Msg.Required), MaxLength(200, ErrorMessage = Msg.MaxLength)] string Name,
    [Required(ErrorMessage = Msg.Required), MaxLength(20, ErrorMessage = Msg.MaxItems), NoNullItems, ItemMaxLength(100)] IReadOnlyList<string> Genres,
    [MaxLength(500, ErrorMessage = Msg.MaxLength)] string? ImageUrl = null);
