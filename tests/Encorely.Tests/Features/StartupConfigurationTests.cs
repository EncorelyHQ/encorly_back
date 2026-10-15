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
    public void Limites_de_login_se_configuran_por_variable_de_entorno()
    {
        // Valores iguales a los de por defecto: la variable de proceso es compartida con las demás pruebas en paralelo.
        (string Env, string Key, string Value)[] vars =
        [
            ("RATE_LIMIT_AUTH_PER_MINUTE", "RateLimit:AuthPermitPerMinute", "10"),
            ("AUTH_MAX_FAILED_LOGINS", "RateLimit:MaxFailedLogins", "5"),
            ("AUTH_LOCKOUT_MINUTES", "RateLimit:LockoutMinutes", "15"),
        ];
        var previous = vars.Select(v => Environment.GetEnvironmentVariable(v.Env)).ToArray();
        try
        {
            foreach (var v in vars) Environment.SetEnvironmentVariable(v.Env, v.Value);
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddEncorelyEnvironment().Build();
            foreach (var v in vars) Assert.Equal(v.Value, config[v.Key]);
        }
        finally
        {
            for (var i = 0; i < vars.Length; i++) Environment.SetEnvironmentVariable(vars[i].Env, previous[i]);
        }
    }

    [Fact]
    public void DotEnv_no_sube_mas_alla_de_la_raiz_del_repo()
    {
        var root = Path.Combine(Path.GetTempPath(), $"encorely-dotenv-{Guid.NewGuid():N}");
        var repo = Path.Combine(root, "repo");
        var api = Path.Combine(repo, "src", "api");
        Directory.CreateDirectory(api);
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var key = $"ENCORELY_TEST_{Guid.NewGuid():N}";
        File.WriteAllText(Path.Combine(root, ".env"), $"{key}=fuera-del-repo");
        try
        {
            DotEnv.LoadFromNearest(api);
            Assert.Null(Environment.GetEnvironmentVariable(key));

            File.WriteAllText(Path.Combine(repo, ".env"), $"{key}=del-repo");
            DotEnv.LoadFromNearest(api);
            Assert.Equal("del-repo", Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Environment.SetEnvironmentVariable(key, null);
        }
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
