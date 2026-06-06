using System.ComponentModel.DataAnnotations;

namespace EncorelyApplication.DTOs;

public record DnaMixRequest(
    [property: Required] Guid UserId1,
    [property: Required] Guid UserId2,
    [property: Required] string AccessToken1,
    [property: Required] string AccessToken2
);
