using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Encorely.Api.Infrastructure.Database;

/// <summary>Solo para `dotnet ef migrations add`: no necesita una BD real ni secretos.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=encorely_design")
        .UseSnakeCaseNamingConvention()
        .Options);
}
