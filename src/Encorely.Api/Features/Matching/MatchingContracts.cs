using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Matching;

/// <param name="EventId">Evento donde se vieron (opcional, contexto del match).</param>
public sealed record LikeRequest([NotEmptyGuid] Guid TargetUserId, Guid? EventId = null);

public sealed record LikeResponse(bool Matched, Guid? MatchId);

public sealed record PeerDto(Guid Id, string DisplayName, int? Age, string? City);

/// <param name="LastSeq">Secuencia del último mensaje (0 si no hay).</param>
public sealed record MatchDto(
    Guid Id,
    PeerDto Peer,
    string? EventName,
    IReadOnlyList<string> SharedGenres,
    MatchStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt,
    long LastSeq);
