using Encorely.Api.Infrastructure.Configuration;
using Encorely.Tests.Infrastructure;

namespace Encorely.Tests.Features;

/// <summary>FIN-07: la configuración que falla en silencio en producción ahora impide arrancar.</summary>
public sealed class StartupConfigurationTests
{
    [Theory]
    [InlineData("Production", "", false)]
    [InlineData("Production", "10.0.0.0/8", true)]
    [InlineData("Production", "no-es-cidr", false)]
    [InlineData("Development", "", true)]
    [InlineData("Development", "no-es-cidr", false)]
    [InlineData("Testing", "", true)]
    public void Proxy_de_confianza_es_obligatorio_fuera_de_desarrollo(string environment, string networks, bool ok)
    {
        var result = new ProxyOptionsValidator(new TestEnvironment(environment)).Validate(null, new ProxyOptions { TrustedNetworks = networks });
        Assert.Equal(ok, result.Succeeded);
    }

    [Theory]
    [InlineData("Production", null, false)]
    [InlineData("Production", "http://api.encorely.app", false)]
    [InlineData("Production", "/relativa", false)]
    [InlineData("Production", "https://api.encorely.app", true)]
    [InlineData("Development", null, true)]
    [InlineData("Testing", null, true)]
    public void Url_publica_https_es_obligatoria_fuera_de_desarrollo(string environment, string? baseUrl, bool ok)
    {
        var result = new LegalOptionsValidator(new TestEnvironment(environment)).Validate(null, new LegalOptions { PublicBaseUrl = baseUrl });
        Assert.Equal(ok, result.Succeeded);
    }

    [Fact]
    public void DotEnv_corta_comentarios_inline_como_docker_compose()
    {
        var prefix = $"ENCORELY_TEST_{Guid.NewGuid():N}_";
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}.env");
        File.WriteAllLines(path,
        [
            $"{prefix}A=secreto   # elige una",
            $"{prefix}B=con#almohadilla",
            $"{prefix}C=\"entre comillas # se conserva\"",
            $"{prefix}D=   # solo comentario",
        ]);
        try
        {
            DotEnv.Load(path);
            Assert.Equal("secreto", Environment.GetEnvironmentVariable($"{prefix}A"));
            Assert.Equal("con#almohadilla", Environment.GetEnvironmentVariable($"{prefix}B"));
            Assert.Equal("entre comillas # se conserva", Environment.GetEnvironmentVariable($"{prefix}C"));
            Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable($"{prefix}D")));
        }
        finally
        {
            File.Delete(path);
            foreach (var k in "ABCD") Environment.SetEnvironmentVariable($"{prefix}{k}", null);
        }
    }
}
