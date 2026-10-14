using System.Net.Http.Headers;
using Encorely.Api.Data;
using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Encorely.Tests.Infrastructure.ApiFixture))]

namespace Encorely.Tests.Infrastructure;

/// <summary>
/// Un Postgres real (Testcontainers) y una API en memoria para todo el ensamblado. Las pruebas no se pisan
/// porque cada una crea sus propios usuarios con emails únicos.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string JwtKey = "clave-de-pruebas-solo-para-tests-0123456789abcdef";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public string ConnectionString => _postgres.GetConnectionString();
    public FakeSpotify Spotify { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        var cs = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString());

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = cs.Host,
                ["Database:Port"] = cs.Port.ToString(),
                ["Database:Name"] = cs.Database,
                ["Database:User"] = cs.Username,
                ["Database:Password"] = cs.Password,
                ["Database:SslMode"] = "Disable",
                ["Database:MigrateOnStartup"] = "false",
                ["Seed:Catalog"] = "false",
                ["Seed:DemoUserPassword"] = "",
                ["Jwt:SigningKey"] = JwtKey,
                ["RateLimit:AuthPermitPerMinute"] = "10000",
                ["RateLimit:UserWritesPerMinute"] = "100000",
                ["Push:Enabled"] = "false",
                ["Matching:RadarMinSwipes"] = "3",
                ["Spotify:ClientId"] = FakeSpotify.ClientId,
                ["Spotify:AllowedRedirects"] = FakeSpotify.RedirectUri,
            }));
            b.ConfigureTestServices(s => s.AddHttpClient<SpotifyClient>().ConfigurePrimaryHttpMessageHandler(Spotify.CreateHandler));
        });

        // Migraciones y catálogo explícitos (no en paralelo con el arranque de la app).
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await ActivatorUtilities.CreateInstance<Seeder>(scope.ServiceProvider, Options.Create(new SeedOptions { Catalog = true })).SeedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Cliente sin sesión.</summary>
    public HttpClient Anonymous() => Factory.CreateClient();

    /// <summary>Contraseña de todos los usuarios de <see cref="CreateUserAsync"/> (costo bajo: solo para tests).</summary>
    public const string TestPassword = "contraseña-de-prueba-1";
    private static readonly string TestPasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword, 4);

    /// <summary>
    /// Crea un usuario directo en la BD (onboarding completo por defecto) y devuelve un cliente con su JWT.
    /// No depende de /auth: cada feature se prueba aislada.
    /// </summary>
    public async Task<TestUser> CreateUserAsync(string? displayName = null, Action<User>? configure = null, params string[] genres)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slugs = genres.Length > 0 ? genres : ["reggaeton"];
        var genreIds = await db.Genres.Where(g => slugs.Contains(g.Slug)).Select(g => g.Id).ToListAsync();

        var user = new User
        {
            Email = $"u{Guid.NewGuid():N}@test.encorely.local",
            PasswordHash = TestPasswordHash,
            DisplayName = displayName ?? "Tester",
            BirthDate = new DateOnly(1999, 5, 20),
            City = "Bogotá",
            Genres = genreIds.Select(id => new UserGenre { GenreId = id }).ToList(),
        };
        configure?.Invoke(user);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var (_, token, client) = await OpenSessionAsync(user.Id);
        return new TestUser(user.Id, user.Email, user.DisplayName, token, client);
    }

    /// <summary>
    /// Abre otra sesión (familia de refresh) del usuario, como un segundo dispositivo, y devuelve su JWT con el claim sid.
    /// El access token solo sirve mientras esa sesión siga abierta.
    /// </summary>
    public async Task<(Guid SessionId, string AccessToken, HttpClient Client)> OpenSessionAsync(Guid userId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = new RefreshToken
        {
            UserId = userId, FamilyId = Guid.NewGuid(), TokenHash = TokenService.CreateRefreshToken().Hash, ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        };
        db.RefreshTokens.Add(session);
        await db.SaveChangesAsync();

        var user = await db.Users.SingleAsync(u => u.Id == userId);
        var (token, _) = scope.ServiceProvider.GetRequiredService<TokenService>().CreateAccessToken(user, session.FamilyId);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (session.FamilyId, token, client);
    }

    /// <summary>Acceso directo a la BD para preparar o verificar estado.</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public T Service<T>() where T : notnull => Factory.Services.GetRequiredService<T>();
}

public sealed record TestUser(Guid Id, string Email, string DisplayName, string AccessToken, HttpClient Client);
