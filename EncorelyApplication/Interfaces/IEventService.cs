namespace EncorelyApplication.Interfaces;

public interface IEventService
{
    Task<IEnumerable<object>> GetNearbyEventsAsync(CancellationToken ct = default);
}
