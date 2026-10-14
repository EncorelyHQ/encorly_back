using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Events;

/// <summary>Radar por evento. Única consulta con Dapper: afinidad calculada en SQL sobre los asistentes.</summary>
public sealed class RadarService(AppDbContext db, IOptions<MatchingOptions> matching, TimeProvider clock)
{
    public Task<IReadOnlyList<RadarEntryDto>> GetAsync(Guid userId, Guid eventId, CancellationToken ct) => throw new NotImplementedException();
}
