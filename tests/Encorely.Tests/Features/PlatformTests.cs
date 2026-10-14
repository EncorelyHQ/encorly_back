using System.Net;
using System.Text.Json;
using Encorely.Tests.Infrastructure;

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
