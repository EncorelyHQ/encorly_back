using System.Text.Json.Serialization;

namespace EncorelyApplication.DTOs;

public record SpotifyAuthRequest(
    [property: JsonPropertyName("accessToken")] string AccessToken
);
