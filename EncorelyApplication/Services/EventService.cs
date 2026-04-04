using EncorelyApplication.Interfaces;

namespace EncorelyApplication.Services;

public class EventService : IEventService
{
    public async Task<IEnumerable<object>> GetNearbyEventsAsync(CancellationToken ct = default)
    {
        // Mock Ticketmaster Feed
        return await Task.FromResult(new List<object>
        {
            new { Id = "TM_001", Name = "Lollapalooza 2026", Venue = "Grant Park", Date = "2026-08-01", AffiliateUrl = "https://ticketmaster.com/..." },
            new { Id = "TM_002", Name = "Arctic Monkeys World Tour", Venue = "O2 Arena", Date = "2026-09-15", AffiliateUrl = "https://ticketmaster.com/..." }
        });
    }
}
