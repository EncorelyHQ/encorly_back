namespace Encorely.Api.Infrastructure.Http;

/// <summary>
/// Error de negocio esperado. Se traduce a ProblemDetails con <c>code</c> estable para que el cliente
/// ramifique sin parsear texto. Nunca lleva datos internos.
/// </summary>
public sealed class ApiException(int status, string code, string title, string? detail = null) : Exception(title)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public string Title { get; } = title;
    public string? Detail { get; } = detail;

    public static ApiException NotFound(string code, string title) => new(StatusCodes.Status404NotFound, code, title);
    public static ApiException Forbidden(string code, string title, string? detail = null) => new(StatusCodes.Status403Forbidden, code, title, detail);
    public static ApiException Conflict(string code, string title, string? detail = null) => new(StatusCodes.Status409Conflict, code, title, detail);
    public static ApiException BadRequest(string code, string title, string? detail = null) => new(StatusCodes.Status400BadRequest, code, title, detail);
    public static ApiException Unauthorized(string code, string title) => new(StatusCodes.Status401Unauthorized, code, title);
}

/// <summary>Códigos de error estables publicados en el contrato.</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string InvalidBody = "invalid_body";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string RateLimited = "rate_limited";
    public const string Internal = "internal_error";

    public const string EmailTaken = "email_taken";
    public const string InvalidCredentials = "invalid_credentials";
    public const string InvalidPassword = "invalid_password";
    public const string AccountLocked = "account_locked";
    public const string AccountBanned = "account_banned";
    public const string InvalidRefreshToken = "invalid_refresh_token";
    public const string ConsentRequired = "consent_required";
    public const string UnderAge = "under_age";

    public const string SongNotFound = "song_not_found";
    public const string UnknownGenre = "unknown_genre";
    public const string EventNotFound = "event_not_found";
    public const string EventPast = "event_past";
    public const string NotAttending = "not_attending";
    public const string RadarLocked = "radar_locked";

    public const string UserNotFound = "user_not_found";
    public const string CannotLikeSelf = "cannot_like_self";
    public const string NoSharedEvent = "no_shared_event";
    public const string Blocked = "blocked";
    public const string MatchNotFound = "match_not_found";
    public const string MatchClosed = "match_closed";

    public const string KeysMissing = "keys_missing";
    public const string PeerKeysMissing = "peer_keys_missing";
    public const string StaleBoxKeyId = "stale_box_key_id";
    public const string StaleRecipientKey = "stale_recipient_key";
    public const string UnknownSenderKey = "unknown_sender_key";
    public const string MessageIdConflict = "message_id_conflict";
}
