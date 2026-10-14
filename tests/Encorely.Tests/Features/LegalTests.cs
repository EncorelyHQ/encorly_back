using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Encorely.Api.Features.Legal;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Tests.Features;

public sealed class LegalTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string> RegisterAsync(ApiFixture api, string email, string password)
    {
        var consent = api.Service<IOptions<LegalOptions>>().Value.ConsentVersion;
        await api.Anonymous().PostJson("/api/v1/auth/register",
            new { email, password, displayName = "Ana", acceptTerms = true, consentVersion = consent }).Expect(HttpStatusCode.OK);
        return email;
    }

    [Fact]
    public async Task Legal_current_es_publico_y_devuelve_version_y_urls()
    {
        var legal = await api.Anonymous().GetAsync("/api/v1/legal/current", Ct).Expect<LegalResponse>(HttpStatusCode.OK);
        var options = api.Service<IOptions<LegalOptions>>().Value;
        // Sin PublicBaseUrl la base sale del host de la request (el cliente de pruebas apunta a http://localhost).
        var baseUrl = "http://localhost";

        Assert.Equal(options.ConsentVersion, legal.ConsentVersion);
        Assert.Equal(options.EffectiveDate, legal.EffectiveDate);
        Assert.Equal($"{baseUrl}/legal/terminos", legal.TermsUrl);
        Assert.Equal($"{baseUrl}/legal/privacidad", legal.PrivacyUrl);
        Assert.Equal($"{baseUrl}/cuenta/eliminar", legal.AccountDeletionUrl);
    }

    [Fact]
    public async Task Account_deletion_con_credenciales_correctas_borra_la_cuenta()
    {
        var email = await RegisterAsync(api, $"{Guid.NewGuid():N}@mail.com", "contraseña-segura-1");

        await api.Anonymous().PostJson("/api/v1/account-deletion", new { email = $"  {email.ToUpperInvariant()} ", password = "contraseña-segura-1" })
            .Expect(HttpStatusCode.NoContent);

        Assert.False(await api.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == email, Ct)));
        await api.Anonymous().PostJson("/api/v1/auth/login", new { email, password = "contraseña-segura-1" })
            .ExpectProblem(HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Fact]
    public async Task Account_deletion_con_credenciales_malas_da_401_igual_y_no_borra_nada()
    {
        var email = await RegisterAsync(api, $"{Guid.NewGuid():N}@mail.com", "contraseña-segura-1");
        var other = await RegisterAsync(api, $"{Guid.NewGuid():N}@mail.com", "contraseña-segura-2");

        var wrong = await api.Anonymous().PostJson("/api/v1/account-deletion", new { email, password = "contraseña-segura-2" })
            .ExpectProblem(HttpStatusCode.Unauthorized, "invalid_credentials");
        var unknown = await api.Anonymous().PostJson("/api/v1/account-deletion", new { email = $"{Guid.NewGuid():N}@mail.com", password = "contraseña-segura-1" })
            .ExpectProblem(HttpStatusCode.Unauthorized, "invalid_credentials");

        Assert.Equal(wrong.GetProperty("title").GetString(), unknown.GetProperty("title").GetString());
        Assert.Equal(2, await api.WithDbAsync(db => db.Users.CountAsync(u => u.Email == email || u.Email == other, Ct)));
    }

    [Fact]
    public async Task Account_deletion_con_cuerpo_invalido_da_400()
    {
        await api.Anonymous().PostJson("/api/v1/account-deletion", new { email = "a@b.com" }).ExpectProblem(HttpStatusCode.BadRequest);
        await api.Anonymous().PostJson("/api/v1/account-deletion", new { email = "a@b.com", password = new string('a', 73) }).ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("/cuenta/eliminar", true)]
    [InlineData("/legal/terminos", false)]
    [InlineData("/legal/privacidad", false)]
    public async Task Paginas_html_son_publicas_con_su_propia_CSP(string path, bool hasScript)
    {
        var response = await api.Anonymous().GetAsync(path, Ct).Expect(HttpStatusCode.OK);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var csp = string.Join(' ', response.Headers.GetValues("Content-Security-Policy"));
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
        Assert.Contains("frame-ancestors 'none'", csp);

        var html = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("<html lang=\"es\">", html);
        Assert.Contains("proyecto académico", html);

        // Los hashes de la CSP deben corresponder exactamente al contenido inline que se sirve.
        var style = Regex.Match(html, "<style>(.*?)</style>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Contains($"'{Sha256(style)}'", csp);
        var script = Regex.Match(html, "<script>(.*?)</script>", RegexOptions.Singleline);
        Assert.Equal(hasScript, script.Success);
        if (hasScript) Assert.Contains($"script-src '{Sha256(script.Groups[1].Value)}'", csp);
        else Assert.DoesNotContain("script-src", csp);
    }

    [Fact]
    public async Task Pagina_de_borrado_tiene_formulario_y_llama_al_endpoint()
    {
        var html = await api.Anonymous().GetStringAsync("/cuenta/eliminar", Ct);
        Assert.Contains("type=\"email\"", html);
        Assert.Contains("type=\"password\"", html);
        Assert.Contains("/api/v1/account-deletion", html);
    }

    [Fact]
    public async Task Politica_de_privacidad_cita_la_ley_la_version_y_el_cifrado_de_extremo_a_extremo()
    {
        var options = api.Service<IOptions<LegalOptions>>().Value;
        var html = await api.Anonymous().GetStringAsync("/legal/privacidad", Ct);
        Assert.Contains("Ley 1581 de 2012", html);
        Assert.Contains("extremo a extremo", html);
        Assert.Contains("18 años", html);
        Assert.Contains(options.ConsentVersion, html);
        Assert.Contains(options.EffectiveDate.ToString("yyyy-MM-dd"), html);
    }

    private static string Sha256(string content) => "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
