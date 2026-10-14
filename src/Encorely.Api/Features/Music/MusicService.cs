using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Music;

public sealed class MusicService(AppDbContext db, IOptions<MatchingOptions> matching)
{
    public Task<IReadOnlyList<GenreDto>> GetGenresAsync(CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<SongDto>> GetFeedAsync(Guid userId, int limit, CancellationToken ct) => throw new NotImplementedException();
    public Task<SwipeResponse> SwipeAsync(Guid userId, SwipeRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<DnaResponse> GetDnaAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
    public Task<DnaResponse> ImportSpotifyAsync(Guid userId, SpotifyImportRequest request, CancellationToken ct) => throw new NotImplementedException();
}
