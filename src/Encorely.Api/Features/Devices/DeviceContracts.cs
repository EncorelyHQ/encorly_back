using System.ComponentModel.DataAnnotations;
using Encorely.Api.Infrastructure.Http;

namespace Encorely.Api.Features.Devices;

/// <param name="Token">ExpoPushToken[...].</param>
public sealed record PushTokenRequest(
    [Required(ErrorMessage = Msg.Required), MaxLength(200, ErrorMessage = Msg.MaxLength)] string Token,
    DevicePlatform Platform);

public sealed record DeletePushTokenRequest([Required(ErrorMessage = Msg.Required), MaxLength(200, ErrorMessage = Msg.MaxLength)] string Token);
