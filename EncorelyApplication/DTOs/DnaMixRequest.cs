using System.ComponentModel.DataAnnotations;

namespace EncorelyApplication.DTOs;

public record DnaMixRequest(
    [Required] Guid UserId1,
    [Required] Guid UserId2,
    [Required] string AccessToken1,
    [Required] string AccessToken2
);
