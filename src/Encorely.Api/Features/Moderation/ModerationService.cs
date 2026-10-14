using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Realtime;

namespace Encorely.Api.Features.Moderation;

public sealed class ModerationService(AppDbContext db, Notifier notifier, TimeProvider clock)
{
    public Task BlockAsync(Guid userId, BlockRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<BlockedUserDto>> ListBlocksAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
    public Task UnblockAsync(Guid userId, Guid blockedId, CancellationToken ct) => throw new NotImplementedException();
    public Task<ReportCreatedDto> ReportAsync(Guid userId, ReportRequest request, CancellationToken ct) => throw new NotImplementedException();
}
