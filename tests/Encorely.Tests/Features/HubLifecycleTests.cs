using System.Net;
using Encorely.Api.Features.Users;
using Encorely.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;

namespace Encorely.Tests.Features;

public sealed class HubLifecycleTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<TaskCompletionSource> ConnectAsync(HubConnection hub)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.Closed += _ => { closed.TrySetResult(); return Task.CompletedTask; };
        await hub.StartAsync(Ct);
        Assert.Equal(HubConnectionState.Connected, hub.State);
        return closed;
    }

    [Fact]
    public async Task Suspender_la_cuenta_corta_su_conexion_al_hub()
    {
        var admin = await api.CreateUserAsync("Admin", u => u.Role = UserRole.Admin);
        var user = await api.CreateUserAsync();
        await using var hub = ChatKit.Hub(api, user.AccessToken);
        var closed = await ConnectAsync(hub);

        await admin.Client.PutJson($"/api/v1/admin/users/{user.Id}/status", new { status = "banned" }).Expect(HttpStatusCode.NoContent);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
    }

    [Fact]
    public async Task Borrar_la_cuenta_corta_su_conexion_al_hub()
    {
        var user = await api.CreateUserAsync();
        await using var hub = ChatKit.Hub(api, user.AccessToken);
        var closed = await ConnectAsync(hub);

        await user.Client.DeleteMe().Expect(HttpStatusCode.NoContent);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
    }
}
