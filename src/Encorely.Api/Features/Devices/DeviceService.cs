using Encorely.Api.Infrastructure.Database;

namespace Encorely.Api.Features.Devices;

public sealed class DeviceService(AppDbContext db, TimeProvider clock)
{
    public Task RegisterAsync(Guid userId, PushTokenRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task UnregisterAsync(Guid userId, DeletePushTokenRequest request, CancellationToken ct) => throw new NotImplementedException();
}
