using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Realtime;

namespace Encorely.Api.Features.Chat;

public sealed class KeyService(AppDbContext db, Notifier notifier, TimeProvider clock)
{
    public Task<KeyBundleDto> PutAsync(Guid userId, UploadKeysRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<KeyBundleDto> GetMineAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
    public Task<KeyBundleDto> GetPeerAsync(Guid userId, Guid matchId, CancellationToken ct) => throw new NotImplementedException();
    public Task<BoxKeyDto> GetPeerBoxKeyAsync(Guid userId, Guid matchId, int boxKeyId, CancellationToken ct) => throw new NotImplementedException();
}
