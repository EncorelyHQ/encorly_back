using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EncorelyInfrastructure.Persistence;

/// <summary>Allows EF Core tooling (dotnet ef) to instantiate the DbContext without the full DI container.</summary>
public class EncorelyDbContextFactory : IDesignTimeDbContextFactory<EncorelyDbContext>
{
    public EncorelyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EncorelyDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=encorely_db;Username=admin;Password=Encorely2026!");
        return new EncorelyDbContext(optionsBuilder.Options);
    }
}
