using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Legal;
using Encorely.Api.Features.Sessions;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Encorely.Tests.Features;

/// <summary>Pedidos del front de v1.0.0-draft.6: URLs legales por host, estado de consentimiento, sesión actual y contacto de privacidad.</summary>
public sealed class FrontRequestsTests(ApiFixture api)
{
    private const string Password = "contraseña-segura-1";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LegalOptions Legal => api.Service<IOptions<LegalOptions>>().Value;

    private WebApplicationFactory<Program> With(params (string Key, string? Value)[] settings)
        => api.Factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value))));

    private static HttpClient Bearer(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<AuthResponse> RegisterAsync(string? email = null)
        => await api.Anonymous().PostJson("/api/v1/auth/register",
            new { email = email ?? $"{Guid.NewGuid():N}@mail.com", password = Password, displayName = "Ana", acceptTerms = true, consentVersion = Legal.ConsentVersion })
            .Expect<AuthResponse>(HttpStatusCode.OK);

    private static async Task<LegalResponse> LegalFor(HttpClient client, string host)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/legal/current");
        request.Headers.Host = host;
        return await client.SendAsync(request, Ct).Expect<LegalResponse>(HttpStatusCode.OK);
    }

    private static string Sha256(string content) => "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    // ---------- 1. URLs legales ----------

    [Fact]
    public async Task Legal_sin_base_configurada_deriva_las_urls_del_host_de_la_request()
    {
        Assert.True(string.IsNullOrWhiteSpace(Legal.PublicBaseUrl));
        var client = api.Anonymous();

        var emulator = await LegalFor(client, "10.0.2.2:5000");
        Assert.Equal("http://10.0.2.2:5000/legal/terminos", emulator.TermsUrl);
        Assert.Equal("http://10.0.2.2:5000/legal/privacidad", emulator.PrivacyUrl);
        Assert.Equal("http://10.0.2.2:5000/cuenta/eliminar", emulator.AccountDeletionUrl);

        var other = await LegalFor(client, "api.ejemplo.test");
        Assert.Equal("http://api.ejemplo.test/legal/terminos", other.TermsUrl);
    }

    [Fact]
    public async Task Legal_con_base_configurada_la_usa_tal_cual_sin_importar_el_host()
    {
        await using var factory = With(("Legal:PublicBaseUrl", "https://encorely.example.com/"));

        var legal = await LegalFor(factory.CreateClient(), "10.0.2.2:5000");

        Assert.Equal("https://encorely.example.com/legal/terminos", legal.TermsUrl);
        Assert.Equal("https://encorely.example.com/legal/privacidad", legal.PrivacyUrl);
        Assert.Equal("https://encorely.example.com/cuenta/eliminar", legal.AccountDeletionUrl);
    }

    // ---------- 4. Contacto de privacidad ----------

    [Fact]
    public async Task Sin_contacto_configurado_legal_da_null_y_la_pagina_deja_el_texto_generico()
    {
        var legal = await api.Anonymous().GetAsync("/api/v1/legal/current", Ct).Expect<LegalResponse>(HttpStatusCode.OK);
        Assert.Null(legal.PrivacyContactEmail);

        var html = await api.Anonymous().GetStringAsync("/legal/privacidad", Ct);
        Assert.Contains("contacto publicado en la ficha de la aplicación", html);
        Assert.DoesNotContain("mailto:", html);
    }

    [Fact]
    public async Task Con_contacto_configurado_legal_lo_expone_y_la_pagina_lo_usa_con_CSP_valida()
    {
        await using var factory = With(("Legal:PrivacyContactEmail", "datos@encorely.example"));
        var client = factory.CreateClient();

        var legal = await client.GetAsync("/api/v1/legal/current", Ct).Expect<LegalResponse>(HttpStatusCode.OK);
        Assert.Equal("datos@encorely.example", legal.PrivacyContactEmail);

        var response = await client.GetAsync("/legal/privacidad", Ct).Expect(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("<a href=\"mailto:datos@encorely.example\">datos@encorely.example</a>", html);
        Assert.DoesNotContain("contacto publicado en la ficha de la aplicación", html);

        var csp = string.Join(' ', response.Headers.GetValues("Content-Security-Policy"));
        var style = Regex.Match(html, "<style>(.*?)</style>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Contains($"'{Sha256(style)}'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
    }

    [Theory]
    [InlineData("no-es-un-email")]
    [InlineData("a b@encorely.example")]
    [InlineData("<script>@x.co")]
    public void Contacto_invalido_impide_arrancar(string email)
    {
        using var factory = With(("Legal:PrivacyContactEmail", email));

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("PRIVACY_CONTACT_EMAIL", ex.ToString());
    }

    // ---------- 2. Estado de consentimiento en /me ----------

    [Fact]
    public async Task Me_trae_el_consentimiento_aceptado_y_se_actualiza_al_cambiar_la_version_vigente()
    {
        var auth = await RegisterAsync();
        var me = await Bearer(api.Factory, auth.AccessToken).GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Equal(Legal.ConsentVersion, me.AcceptedConsentVersion);
        Assert.True(me.ConsentUpToDate);

        const string next = "2099-01-01";
        await using var factory = With(("Legal:ConsentVersion", next));
        var client = Bearer(factory, auth.AccessToken);

        var stale = await client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Equal(Legal.ConsentVersion, stale.AcceptedConsentVersion);
        Assert.False(stale.ConsentUpToDate);

        await client.PostJson("/api/v1/me/consents", new { version = next }).Expect(HttpStatusCode.NoContent);
        var fresh = await client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Equal(next, fresh.AcceptedConsentVersion);
        Assert.True(fresh.ConsentUpToDate);
    }

    [Fact]
    public async Task Me_sin_consentimientos_registrados_da_null_y_false()
    {
        var user = await api.CreateUserAsync();

        var me = await user.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);

        Assert.Null(me.AcceptedConsentVersion);
        Assert.False(me.ConsentUpToDate);
    }

    // ---------- 3. Sesión actual ----------

    [Fact]
    public async Task Sessions_marca_current_solo_la_del_access_token_usado()
    {
        var email = $"{Guid.NewGuid():N}@mail.com";
        await RegisterAsync(email);
        var first = await api.Anonymous().PostJson("/api/v1/auth/login", new { email, password = Password }).Expect<AuthResponse>(HttpStatusCode.OK);
        var second = await api.Anonymous().PostJson("/api/v1/auth/login", new { email, password = Password }).Expect<AuthResponse>(HttpStatusCode.OK);

        foreach (var auth in new[] { first, second })
        {
            var sid = Guid.Parse(new JsonWebToken(auth.AccessToken).GetPayloadValue<string>("sid"));
            var sessions = await Bearer(api.Factory, auth.AccessToken).GetAsync("/api/v1/me/sessions", Ct).Expect<List<SessionDto>>(HttpStatusCode.OK);

            Assert.Equal(3, sessions.Count);
            Assert.Equal(sid, Assert.Single(sessions, s => s.Current).Id);
        }
    }

    // ---------- Borrado de cuenta y push tokens ----------

    [Fact]
    public async Task Borrar_la_cuenta_borra_sus_push_tokens()
    {
        var user = await api.CreateUserAsync();
        var token = $"ExponentPushToken[{Guid.NewGuid():N}]";
        await user.Client.PutJson("/api/v1/devices/push-token", new { token, platform = "android" }).Expect(HttpStatusCode.NoContent);
        Assert.True(await api.WithDbAsync(db => db.DevicePushTokens.AnyAsync(t => t.Token == token, Ct)));

        await user.Client.DeleteAsync("/api/v1/me", Ct).Expect(HttpStatusCode.NoContent);

        Assert.False(await api.WithDbAsync(db => db.DevicePushTokens.AnyAsync(t => t.Token == token, Ct)));
    }
}
