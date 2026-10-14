using System.ComponentModel.DataAnnotations;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Admin;

/// <param name="StartsAt">ISO-8601 con zona (Colombia es UTC-5).</param>
public sealed record AdminEventRequest(
    [Required(ErrorMessage = Msg.Required), StringLength(200, MinimumLength = 3, ErrorMessage = Msg.Length)] string Name,
    [Required(ErrorMessage = Msg.Required), MinLength(1, ErrorMessage = "Agrega al menos un artista."), MaxLength(30, ErrorMessage = Msg.MaxItems), NoNullItems] IReadOnlyList<string> ArtistLineup,
    [Required(ErrorMessage = Msg.Required), StringLength(200, MinimumLength = 2, ErrorMessage = Msg.Length)] string Venue,
    [Required(ErrorMessage = Msg.Required), StringLength(80, MinimumLength = 2, ErrorMessage = Msg.Length)] string City,
    DateTimeOffset StartsAt,
    [Url(ErrorMessage = "No es una URL válida."), MaxLength(500, ErrorMessage = Msg.MaxLength)] string? ImageUrl = null,
    [MaxLength(2000, ErrorMessage = Msg.MaxLength)] string? Description = null,
    bool IsPublished = true);

public sealed record AdminEventDto(
    Guid Id,
    string Name,
    IReadOnlyList<string> ArtistLineup,
    string Venue,
    string City,
    DateTimeOffset StartsAt,
    string? ImageUrl,
    string? Description,
    bool IsPublished,
    int AttendeeCount,
    DateTimeOffset CreatedAt);

/// <param name="Genre">Slug del género.</param>
public sealed record AdminSongRequest(
    [Required(ErrorMessage = Msg.Required), StringLength(200, MinimumLength = 1, ErrorMessage = Msg.Length)] string Title,
    [Required(ErrorMessage = Msg.Required), StringLength(200, MinimumLength = 1, ErrorMessage = Msg.Length)] string Artist,
    [Required(ErrorMessage = Msg.Required), MaxLength(40, ErrorMessage = Msg.MaxLength)] string Genre,
    [Required(ErrorMessage = Msg.Required), Url(ErrorMessage = "No es una URL válida."), MaxLength(500, ErrorMessage = Msg.MaxLength)] string CoverUrl,
    [Url(ErrorMessage = "No es una URL válida."), MaxLength(500, ErrorMessage = Msg.MaxLength)] string? PreviewUrl = null,
    long? ITunesTrackId = null,
    bool IsActive = true);

public sealed record AdminSongDto(
    Guid Id,
    string Title,
    string Artist,
    string Genre,
    string CoverUrl,
    string? PreviewUrl,
    long? ITunesTrackId,
    bool IsActive,
    int Likes,
    int Dislikes);

/// <summary>Resultado de iTunes Search para dar de alta canciones con su preview.</summary>
public sealed record ITunesTrackDto(long ITunesTrackId, string Title, string Artist, string CoverUrl, string? PreviewUrl, string? PrimaryGenre);

public sealed record AdminUserRef(Guid Id, string DisplayName, string Email, UserStatus Status);

public sealed record AdminEvidenceDto(Guid MessageId, string Plaintext, bool Verified);

public sealed record AdminReportDto(
    Guid Id,
    AdminUserRef? Reporter,
    AdminUserRef? Reported,
    Guid? MatchId,
    ReportReason Reason,
    string? Detail,
    IReadOnlyList<AdminEvidenceDto> Evidence,
    ReportStatus Status,
    string? ResolutionNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    int PriorReportsAgainstUser);

public enum ReportAction { Dismiss, Resolve, Ban }

/// <param name="Action">dismiss: sin mérito · resolve: atendido sin sanción · ban: suspende al reportado.</param>
public sealed record ResolveReportRequest(ReportAction Action, [MaxLength(1000, ErrorMessage = Msg.MaxLength)] string? Note = null);

public sealed record UserStatusRequest(UserStatus Status);
