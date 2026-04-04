namespace EncorelyDomain.Entities;

public class Match
{
    public Guid Id { get; set; }
    public Guid UserId1 { get; set; }
    public Guid UserId2 { get; set; }
    public double AffinityScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
