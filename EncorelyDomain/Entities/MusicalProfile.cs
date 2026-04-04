namespace EncorelyDomain.Entities;

public class MusicalProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public double Energy { get; set; }
    public double Danceability { get; set; }
    public double Valence { get; set; }
    public double Tempo { get; set; }
}
