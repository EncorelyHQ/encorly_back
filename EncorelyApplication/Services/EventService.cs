using EncorelyApplication.Interfaces;

namespace EncorelyApplication.Services;

public class EventService : IEventService
{
    public async Task<IEnumerable<object>> GetNearbyEventsAsync(CancellationToken ct = default)
    {
        // Mock Ticketmaster Feed — Tarea 76: Ticketing Partner Integration URLs
        return await Task.FromResult(new List<object>
        {
            new { Id = "TM_001", Name = "Lollapalooza 2026", Venue = "Grant Park, Chicago", Date = "2026-08-01", Mood = "Moshpit", AffiliatePurchaseUrl = "https://www.ticketmaster.com/lollapalooza-2026-tickets/event/TM001" },
            new { Id = "TM_002", Name = "Arctic Monkeys World Tour", Venue = "O2 Arena, London", Date = "2026-09-15", Mood = "VIP", AffiliatePurchaseUrl = "https://www.ticketmaster.com/arctic-monkeys-2026/event/TM002" },
            new { Id = "TM_003", Name = "Festival Estéreo Picnic", Venue = "Bogotá, Colombia", Date = "2026-03-20", Mood = "Chill", AffiliatePurchaseUrl = "https://www.eticket.co/estereopicnic2026" },
            new { Id = "TM_004", Name = "Ultra Music Festival", Venue = "Miami, FL", Date = "2026-03-27", Mood = "Moshpit", AffiliatePurchaseUrl = "https://www.ticketmaster.com/ultra-music-festival-2026/event/TM004" }
        });
    }
}
