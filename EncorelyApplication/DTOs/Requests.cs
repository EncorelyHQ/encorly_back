using System.ComponentModel.DataAnnotations;
using EncorelyModels;

namespace EncorelyApplication.DTOs;

public record TokenRequest(
    [property: Required] string Token
);

public record EmailAuthRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(6)] string Password
);

public record SwipeRequest(
    [property: Required] Guid UserId,
    [property: Required] string TrackId,
    [property: Required] SwipeDirection Direction
);

public record UpdateSettingsRequest(
    [property: Required] Guid UserId,
    [property: Required] ConcertMood Mood
);

public record SendMessageRequest(
    [property: Required, MinLength(1), MaxLength(1000)] string Content
);
