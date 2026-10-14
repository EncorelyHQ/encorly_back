using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Admin;

/// <summary>iTunes Search API (autorizada): búsqueda de canciones con preview de 30 s para el catálogo.</summary>
public sealed class ITunesClient(HttpClient http, ILogger<ITunesClient> logger)
{
    public const string BaseAddress = "https://itunes.apple.com/";

    public async Task<IReadOnlyList<ITunesTrackDto>> SearchAsync(string term, CancellationToken ct)
    {
        term = term.Trim();
        if (term.Length is < 2 or > 100)
            throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "Escribe entre 2 y 100 caracteres para buscar.");

        try
        {
            var url = $"search?term={Uri.EscapeDataString(term)}&country=CO&media=music&entity=song&limit=20";
            var response = await http.GetFromJsonAsync<SearchResponse>(url, ct);
            return (response?.Results ?? [])
                .Where(r => r.TrackId is not null && r.TrackName is not null && r.ArtistName is not null)
                .Select(r => new ITunesTrackDto(
                    r.TrackId!.Value,
                    r.TrackName!,
                    r.ArtistName!,
                    (r.ArtworkUrl100 ?? "").Replace("100x100bb", "600x600bb", StringComparison.Ordinal),
                    r.PreviewUrl,
                    r.PrimaryGenreName))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "iTunes Search no respondió");
            throw new ApiException(StatusCodes.Status502BadGateway, "itunes_unavailable", "iTunes no respondió. Intenta de nuevo.");
        }
    }

    private sealed record SearchResponse([property: JsonPropertyName("results")] List<SearchResult>? Results);

    private sealed record SearchResult(
        [property: JsonPropertyName("trackId")] long? TrackId,
        [property: JsonPropertyName("trackName")] string? TrackName,
        [property: JsonPropertyName("artistName")] string? ArtistName,
        [property: JsonPropertyName("artworkUrl100")] string? ArtworkUrl100,
        [property: JsonPropertyName("previewUrl")] string? PreviewUrl,
        [property: JsonPropertyName("primaryGenreName")] string? PrimaryGenreName);
}
