using EncorelyDomain.Entities;
using Microsoft.EntityFrameworkCore;
using EncorelyApplication.Interfaces;

namespace EncorelyInfrastructure.Persistence;

public class EncorelyDbContext : DbContext, IEncorelyDbContext
{
    public EncorelyDbContext(DbContextOptions<EncorelyDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Swipe> Swipes { get; set; }
    public DbSet<MusicalProfile> MusicalProfiles { get; set; }
    public DbSet<Match> Matches { get; set; }
    public DbSet<Message> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.SpotifyId).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Swipe>(entity =>
        {
            entity.HasOne(s => s.User)
                  .WithMany(u => u.UserSwipes)
                  .HasForeignKey(s => s.UserId);
        });
    }
}
