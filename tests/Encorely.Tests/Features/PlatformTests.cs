using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using Encorely.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Encorely.Tests.Features;

public sealed class PlatformTests(ApiFixture api)
{
    [Fact]
    public async Task Health_live_y_ready_responden_sin_detalles()
    {
        var client = api.Anonymous();
        var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken).Expect(HttpStatusCode.OK);
        var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken).Expect(HttpStatusCode.OK);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Endpoint_protegido_sin_token_da_401_problem_en_espanol()
    {
        var body = await api.Anonymous().GetAsync("/api/v1/me", TestContext.Current.CancellationToken)
            .ExpectProblem(HttpStatusCode.Unauthorized, "unauthorized");
        Assert.Equal("Necesitas iniciar sesión.", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Json_mal_formado_da_400_nunca_500()
    {
        var user = await api.CreateUserAsync();
        var content = new StringContent("{ esto no es json", System.Text.Encoding.UTF8, "application/json");
        await user.Client.PostAsync("/api/v1/swipes", content, TestContext.Current.CancellationToken).ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Enum_desconocido_da_400()
    {
        var user = await api.CreateUserAsync();
        await user.Client.PostJson("/api/v1/swipes", new { songId = Guid.NewGuid(), direction = "superlike" })
            .ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Validacion_responde_400_con_errores_por_campo_en_espanol()
    {
        var body = await api.Anonymous().PostJson("/api/v1/auth/register", new
        {
            email = "no-es-correo",
            password = "corta",
            displayName = "A",
            acceptTerms = true,
            consentVersion = "x",
        }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");

        var errors = body.GetProperty("errors");
        Assert.Equal("No es un correo válido.", errors.GetProperty("email")[0].GetString());
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.True(errors.TryGetProperty("displayName", out _));
    }

    [Fact]
    public async Task Errores_de_binding_dicen_el_campo_sin_eco_del_valor()
    {
        var user = await api.CreateUserAsync();
        var query = await user.Client.GetAsync("/api/v1/matches?limit=abc", TestContext.Current.CancellationToken)
            .ExpectProblem(HttpStatusCode.BadRequest, "invalid_body");
        Assert.Equal("La solicitud no es válida.", query.GetProperty("title").GetString());
        Assert.Contains("campo limit", query.GetProperty("detail").GetString());
        Assert.DoesNotContain("abc", query.GetProperty("detail").GetString());

        var body = await user.Client.PostJson("/api/v1/swipes", new { songId = "no-es-guid", direction = "like" })
            .ExpectProblem(HttpStatusCode.BadRequest, "invalid_body");
        Assert.Contains("campo songId", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Errores_405_y_415_llevan_code_y_titulo_en_espanol()
    {
        var user = await api.CreateUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var method = await user.Client.DeleteAsync("/api/v1/genres", ct).ExpectProblem(HttpStatusCode.MethodNotAllowed, "method_not_allowed");
        Assert.Equal("Método no permitido.", method.GetProperty("title").GetString());

        var plain = new StringContent("{}", System.Text.Encoding.UTF8, "text/plain");
        var media = await user.Client.PostAsync("/api/v1/swipes", plain, ct).ExpectProblem(HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
        Assert.Equal("Tipo de contenido no soportado.", media.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Campo_obligatorio_ausente_da_400()
    {
        var user = await api.CreateUserAsync();
        await user.Client.PostJson("/api/v1/swipes", new { songId = Guid.NewGuid() }).ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_de_usuario_borrado_deja_de_servir()
    {
        var user = await api.CreateUserAsync();
        await api.WithDbAsync(async db =>
        {
            db.Users.Remove(db.Users.Single(u => u.Id == user.Id));
            await db.SaveChangesAsync();
        });
        await user.Client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken).ExpectProblem(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Respuestas_llevan_cabeceras_de_seguridad()
    {
        var r = await api.Anonymous().GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
    }
}

/// <summary>
/// El contrato versionado (contracts/openapi.v1.json) debe coincidir con el que genera el código.
/// Para regenerarlo: UPDATE_CONTRACT=1 dotnet test --filter OpenApiContract (o scripts/update-contract.sh).
/// </summary>
public sealed class OpenApiContractTests(ApiFixture api)
{
    [Fact]
    public async Task OpenApiContract_coincide_con_el_archivo_versionado()
    {
        var json = await api.Anonymous().GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var generated = JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement, new JsonSerializerOptions { WriteIndented = true }) + "\n";
        var path = Path.Combine(RepoRoot(), "contracts", "openapi.v1.json");

        if (Environment.GetEnvironmentVariable("UPDATE_CONTRACT") == "1")
        {
            await File.WriteAllTextAsync(path, generated, TestContext.Current.CancellationToken);
            return;
        }

        Assert.True(File.Exists(path), "Falta contracts/openapi.v1.json: corre scripts/update-contract.sh");
        var committed = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        Assert.True(committed == generated,
            "El contrato cambió y contracts/openapi.v1.json no está actualizado. Corre scripts/update-contract.sh y anota el cambio en contracts/CHANGELOG.md.");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Encorely.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repo (Encorely.slnx).");
    }
}

public sealed class UserRateLimitTests(ApiFixture api)
{
    [Fact]
    public async Task Escrituras_tienen_limite_por_usuario_no_por_ip()
    {
        await using var factory = api.Factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimit:UserWritesPerMinute"] = "2" })));
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        HttpClient Client(TestUser u)
        {
            var c = factory.CreateClient();
            c.DefaultRequestHeaders.Authorization = new("Bearer", u.AccessToken);
            return c;
        }

        var ca = Client(a);
        var body = new { userId = Guid.NewGuid() };
        for (var i = 0; i < 2; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await ca.PostJson("/api/v1/blocks", body)).StatusCode);
        await ca.PostJson("/api/v1/blocks", body).ExpectProblem(HttpStatusCode.TooManyRequests, "rate_limited");
        // Otro usuario desde la misma IP no comparte el cupo.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Client(b).PostJson("/api/v1/blocks", body)).StatusCode);
    }
}

public sealed class SeedTests(ApiFixture api)
{
    [Theory]
    [InlineData("Production", "clave", false, false)]
    [InlineData("Production", "clave", true, true)]
    [InlineData("Production", "", false, true)]
    [InlineData("Staging", "clave", false, false)]
    [InlineData("Development", "clave", false, true)]
    public void Usuarios_demo_fuera_de_desarrollo_exigen_modo_revisor(string environment, string password, bool reviewerOnly, bool ok)
    {
        var result = new Encorely.Api.Infrastructure.Configuration.SeedOptionsValidator(new TestEnvironment(environment))
            .Validate(null, new Encorely.Api.Infrastructure.Configuration.SeedOptions { DemoUserPassword = password, ReviewerOnly = reviewerOnly });
        Assert.Equal(ok, result.Succeeded);
    }

    [Fact]
    public void Modo_revisor_siembra_solo_al_revisor_y_su_par_sin_admin()
    {
        Assert.Contains("admin@encorely.demo", Encorely.Api.Data.Seeder.DemoEmails(reviewerOnly: false));
        Assert.Equal(["revisor@encorely.demo", "valentina.rojas@encorely.demo"], Encorely.Api.Data.Seeder.DemoEmails(reviewerOnly: true).Order());
    }

    [Fact]
    public async Task Usuarios_demo_se_siembran_de_forma_idempotente()
    {
        await api.WithDbAsync(async db =>
        {
            var seeder = new Encorely.Api.Data.Seeder(db,
                api.Service<Encorely.Api.Infrastructure.Auth.PasswordHasher>(),
                Microsoft.Extensions.Options.Options.Create(new Encorely.Api.Infrastructure.Configuration.SeedOptions { DemoUserPassword = "contrasena-de-prueba-demo" }),
                Microsoft.Extensions.Options.Options.Create(new Encorely.Api.Infrastructure.Configuration.LegalOptions()),
                TimeProvider.System,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Encorely.Api.Data.Seeder>.Instance);
            await seeder.SeedAsync(TestContext.Current.CancellationToken);
            await seeder.SeedAsync(TestContext.Current.CancellationToken);
        });

        var count = await api.WithDbAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .CountAsync(db.Users, u => u.Email.EndsWith("@encorely.demo"), TestContext.Current.CancellationToken));
        Assert.Equal(16, count);
    }

    [Fact]
    public async Task Sembrar_dos_veces_deja_un_solo_match_activo_del_revisor_con_likes_reciprocos()
    {
        var ct = TestContext.Current.CancellationToken;
        await api.WithDbAsync(async db =>
        {
            var seeder = new Encorely.Api.Data.Seeder(db,
                api.Service<Encorely.Api.Infrastructure.Auth.PasswordHasher>(),
                Microsoft.Extensions.Options.Options.Create(new Encorely.Api.Infrastructure.Configuration.SeedOptions { DemoUserPassword = "contrasena-de-prueba-demo" }),
                Microsoft.Extensions.Options.Options.Create(new Encorely.Api.Infrastructure.Configuration.LegalOptions()),
                TimeProvider.System,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Encorely.Api.Data.Seeder>.Instance);
            await seeder.SeedAsync(ct);
            await seeder.SeedAsync(ct);
        });

        await api.WithDbAsync(async db =>
        {
            var reviewer = await db.Users.SingleAsync(u => u.Email == "revisor@encorely.demo", ct);
            var matches = await db.Matches.Where(m => m.Status == Encorely.Api.Features.Matching.MatchStatus.Active && (m.UserAId == reviewer.Id || m.UserBId == reviewer.Id)).ToListAsync(ct);
            var match = Assert.Single(matches);
            Assert.True(match.UserAId.CompareTo(match.UserBId) < 0);

            var peer = await db.Users.SingleAsync(u => u.Id == match.PeerOf(reviewer.Id), ct);
            Assert.Equal(Encorely.Api.Features.Users.UserRole.User, peer.Role);
            Assert.EndsWith("@encorely.demo", peer.Email);

            Assert.NotNull(match.EventId);
            var attends = db.EventAttendances.Where(x => x.EventId == match.EventId);
            Assert.True(await attends.AnyAsync(x => x.UserId == reviewer.Id, ct));
            Assert.True(await attends.AnyAsync(x => x.UserId == peer.Id, ct));

            Assert.Equal(2, await db.UserLikes.CountAsync(l => (l.FromUserId == reviewer.Id && l.ToUserId == peer.Id) || (l.FromUserId == peer.Id && l.ToUserId == reviewer.Id), ct));
        });
    }
}
