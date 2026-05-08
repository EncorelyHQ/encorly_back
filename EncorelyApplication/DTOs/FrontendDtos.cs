using System.Text.Json.Serialization;

namespace EncorelyApplication.DTOs;

public record AuthResponseDto(
    [property: JsonPropertyName("jwt")] string Jwt,
    [property: JsonPropertyName("userProfile")] UserProfileDto UserProfile,
    [property: JsonPropertyName("dnaVector")] double[] DnaVector
);

public record UserProfileDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("profileImageUrl")] string? ProfileImageUrl
);

public record TrackDiscoveryDto(
    [property: JsonPropertyName("spotifyId")] string SpotifyId,
    [property: JsonPropertyName("previewUrl")] string? PreviewUrl,
    [property: JsonPropertyName("metadata")] TrackMetadataDto Metadata
);

public record TrackMetadataDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("album")] string Album
);

public record SwipeInteractionDto(
    [property: JsonPropertyName("trackId")] string TrackId,
    [property: JsonPropertyName("direction")] string Direction, // "left" or "right"
    [property: JsonPropertyName("progress")] double Progress
);

public record MatchResponseDto(
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("compatibilityScore")] double CompatibilityScore,
    [property: JsonPropertyName("commonArtists")] List<string> CommonArtists
);

public record EventFeedDto(
    [property: JsonPropertyName("ticketmasterId")] string TicketmasterId,
    [property: JsonPropertyName("venue")] string Venue,
    [property: JsonPropertyName("affiliateUrl")] string AffiliateUrl
);

public record SpotifyAuthDto(
    [property: JsonPropertyName("accessToken")] string AccessToken
);
