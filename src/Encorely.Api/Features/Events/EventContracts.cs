namespace Encorely.Api.Features.Events;

public sealed record EventDto(
    Guid Id,
    string Name,
    IReadOnlyList<string> ArtistLineup,
    string Venue,
    string City,
    DateTimeOffset StartsAt,
    string? ImageUrl,
    string? Description,
    int AttendeeCount,
    bool IAmGoing);

/// <param name="Affinity">Afinidad real 0–100 (coseno entre vectores de géneros).</param>
/// <param name="SharedGenres">Slugs de géneros con peso en ambos ADN (máx. 3, de mayor a menor).</param>
public sealed record RadarEntryDto(
    Guid UserId,
    string DisplayName,
    int? Age,
    string? City,
    int Affinity,
    IReadOnlyList<string> SharedGenres,
    bool LikedByMe,
    bool Matched);
