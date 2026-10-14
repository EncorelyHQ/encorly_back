namespace Encorely.Api.Features.Events;

public sealed class Event
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public List<string> ArtistLineup { get; set; } = [];
    public required string Venue { get; set; }
    public required string City { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EventAttendance
{
    public Guid UserId { get; set; }
    public Guid EventId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
