using System.ComponentModel.DataAnnotations;
using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Devices;

/// <param name="Token">ExponentPushToken[...] o ExpoPushToken[...].</param>
public sealed record PushTokenRequest(
    [Required(ErrorMessage = Msg.Required), MaxLength(200, ErrorMessage = Msg.MaxLength),
     RegularExpression(@"^Expo(nent)?PushToken\[[A-Za-z0-9_\-]{1,180}\]$", ErrorMessage = "Debe ser un token de Expo: ExponentPushToken[…].")] string Token,
    DevicePlatform Platform);

public sealed record DeletePushTokenRequest([Required(ErrorMessage = Msg.Required), MaxLength(200, ErrorMessage = Msg.MaxLength)] string Token);
