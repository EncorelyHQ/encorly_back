using System.ComponentModel.DataAnnotations;
using EncorelyModels;

namespace EncorelyApplication.DTOs;

public record TokenRequest(
    [Required] string Token
);

public record EmailAuthRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password
);

public record SwipeRequest(
    [Required] Guid UserId,
    [Required] string TrackId,
    [Required] SwipeDirection Direction
);

public record UpdateSettingsRequest(
    [Required] Guid UserId,
    [Required] ConcertMood Mood
);

public record SendMessageRequest(
    [Required, MinLength(1), MaxLength(1000)] string Content
);
