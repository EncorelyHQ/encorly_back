namespace Encorely.Api.Features.Devices;

public enum DevicePlatform { Android, Ios }

public sealed class DevicePushToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    /// <summary>ExpoPushToken. Único: si otro usuario lo registra en el mismo teléfono, pasa a él.</summary>
    public required string Token { get; set; }
    public DevicePlatform Platform { get; set; }
    /// <summary>Familia de refresh (claim <c>sid</c>) con la que se registró; al cerrar esa sesión el token se borra. Null: registros previos.</summary>
    public Guid? SessionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
