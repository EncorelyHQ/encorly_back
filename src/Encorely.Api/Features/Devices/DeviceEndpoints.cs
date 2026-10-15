using System.Security.Claims;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Encorely.Api.Features.Devices;

public static class DeviceEndpoints
{
    public static void MapDevices(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/devices").WithTags("Push");

        g.MapPut("/push-token", Register).WithName("RegisterPushToken").WithSummary("Registra el token push de este dispositivo").Problems(400, 401).UserWrites();
        g.MapDelete("/push-token", Unregister).WithName("UnregisterPushToken").WithSummary("Quita el token push (al cerrar sesión)").Problems(400, 401);
    }

    private static async Task<NoContent> Register(PushTokenRequest request, ClaimsPrincipal user, DeviceService devices, CancellationToken ct)
    {
        await devices.RegisterAsync(user.UserId(), user.SessionId(), request, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> Unregister([FromBody] DeletePushTokenRequest request, ClaimsPrincipal user, DeviceService devices, CancellationToken ct)
    {
        await devices.UnregisterAsync(user.UserId(), request, ct);
        return TypedResults.NoContent();
    }
}
