using Encorely.Api.Infrastructure.Database;

namespace Encorely.Api.Features.Events;

public sealed class EventService(AppDbContext db, TimeProvider clock)
{
    public Task<IReadOnlyList<EventDto>> ListAsync(Guid userId, string? city, int limit, CancellationToken ct) => throw new NotImplementedException();
    public Task<EventDto> GetAsync(Guid userId, Guid eventId, CancellationToken ct) => throw new NotImplementedException();
    public Task<EventDto> AttendAsync(Guid userId, Guid eventId, CancellationToken ct) => throw new NotImplementedException();
    public Task<EventDto> UnattendAsync(Guid userId, Guid eventId, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<EventDto>> MyEventsAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
}
