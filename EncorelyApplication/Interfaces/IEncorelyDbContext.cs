using Microsoft.EntityFrameworkCore;
using EncorelyDomain.Entities;

namespace EncorelyApplication.Interfaces;

public interface IEncorelyDbContext
{
    DbSet<User> Users { get; set; }
    DbSet<MusicalProfile> MusicalProfiles { get; set; }
    DbSet<Swipe> Swipes { get; set; }
    DbSet<Match> Matches { get; set; }
    DbSet<Message> Messages { get; set; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
