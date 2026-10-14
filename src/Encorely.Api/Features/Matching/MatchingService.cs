using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Realtime;

namespace Encorely.Api.Features.Matching;

public sealed class MatchingService(AppDbContext db, Notifier notifier, TimeProvider clock)
{
    public Task<LikeResponse> LikeAsync(Guid userId, LikeRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<MatchDto>> ListAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
    public Task<MatchDto> GetAsync(Guid userId, Guid matchId, CancellationToken ct) => throw new NotImplementedException();
    public Task UnmatchAsync(Guid userId, Guid matchId, CancellationToken ct) => throw new NotImplementedException();
}
