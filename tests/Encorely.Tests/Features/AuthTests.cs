using System.Net;
using System.Net.Http.Headers;
using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Encorely.Tests.Features;

public sealed class AuthTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string ConsentVersion => api.Service<IOptions<LegalOptions>>().Value.ConsentVersion;

    private static string NewEmail() => $"{Guid.NewGuid():N}@mail.com";

    private object RegisterBody(string email, string password = "contraseña-segura-1", string name = "Ana")
        => new { email, password, displayName = name, acceptTerms = true, consentVersion = ConsentVersion };

    private Task<AuthResponse> RegisterAsync(string email, string password = "contraseña-segura-1")
        => api.Anonymous().PostJson("/api/v1/auth/register", RegisterBody(email, password)).Expect<AuthResponse>(HttpStatusCode.OK);

    private Task<HttpResponseMessage> LoginAsync(string email, string password, HttpClient? client = null)
        => (client ?? api.Anonymous()).PostJson("/api/v1/auth/login", new { email, password });

    private Task<HttpResponseMessage> RefreshAsync(string token) => api.Anonymous().PostJson("/api/v1/auth/refresh", new { refreshToken = token });

    [Fact]
    public async Task Registro_ok_normaliza_email_y_guarda_consentimiento()
    {
        var unique = Guid.NewGuid().ToString("N");
        var auth = await api.Anonymous().PostJson("/api/v1/auth/register", RegisterBody($"  {unique}@MAIL.com ", name: "  Ana  "))
            .Expect<AuthResponse>(HttpStatusCode.OK);

        Assert.Equal($"{unique}@mail.com", auth.User.Email);
        Assert.Equal("Ana", auth.User.DisplayName);
        Assert.False(auth.User.OnboardingCompleted);
        Assert.Equal(UserRole.User, auth.User.Role);
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));

        await api.WithDbAsync(async db =>
        {
            Assert.Equal(ConsentVersion, (await db.UserConsents.SingleAsync(c => c.UserId == auth.User.Id, Ct)).Version);
            Assert.Equal(1, await db.RefreshTokens.CountAsync(t => t.UserId == auth.User.Id, Ct));
            // El refresh se guarda solo como hash.
            Assert.DoesNotContain(await db.RefreshTokens.Where(t => t.UserId == auth.User.Id).ToListAsync(Ct),
                t => t.TokenHash.SequenceEqual(System.Text.Encoding.UTF8.GetBytes(auth.RefreshToken)));
        });
    }

    [Fact]
    public async Task Registro_duplicado_por_mayusculas_da_409()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        await api.Anonymous().PostJson("/api/v1/auth/register", RegisterBody(email.ToUpperInvariant()))
            .ExpectProblem(HttpStatusCode.Conflict, "email_taken");
    }

    [Fact]
    public async Task Registros_concurrentes_con_el_mismo_email_dejan_una_cuenta_y_ningun_500()
    {
        var email = NewEmail();
        var results = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => api.Anonymous().PostJson("/api/v1/auth/register", RegisterBody(email))));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(4, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await api.WithDbAsync(db => db.Users.CountAsync(u => u.Email == email, Ct)));
    }

    [Fact]
    public async Task Registro_sin_consentimiento_o_con_version_vieja_da_400()
    {
        var client = api.Anonymous();
        await client.PostJson("/api/v1/auth/register", new { email = NewEmail(), password = "contraseña-segura-1", displayName = "Ana", acceptTerms = false, consentVersion = ConsentVersion })
            .ExpectProblem(HttpStatusCode.BadRequest, "consent_required");
        var body = await client.PostJson("/api/v1/auth/register", new { email = NewEmail(), password = "contraseña-segura-1", displayName = "Ana", acceptTerms = true, consentVersion = "1999-01-01" })
            .ExpectProblem(HttpStatusCode.BadRequest, "consent_required");
        Assert.Equal("Debes aceptar los términos y la política de datos vigentes.", body.GetProperty("title").GetString());
        await client.PostJson("/api/v1/auth/register", new { email = NewEmail(), password = "contraseña-segura-1", displayName = "Ana", acceptTerms = true })
            .ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Contrasena_de_11_caracteres_o_mas_de_72_bytes_da_400()
    {
        var client = api.Anonymous();
        await client.PostJson("/api/v1/auth/register", RegisterBody(NewEmail(), new string('a', 11))).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await client.PostJson("/api/v1/auth/register", RegisterBody(NewEmail(), new string('a', 73))).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        // 40 caracteres de 2 bytes = 80 bytes.
        await client.PostJson("/api/v1/auth/register", RegisterBody(NewEmail(), new string('ñ', 40))).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await client.PostJson("/api/v1/auth/login", new { email = NewEmail(), password = new string('a', 73) }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await client.PostJson("/api/v1/auth/register", RegisterBody(NewEmail(), new string('a', 12))).Expect(HttpStatusCode.OK);
        await client.PostJson("/api/v1/auth/register", RegisterBody(NewEmail(), new string('a', 72))).Expect(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Nombre_de_solo_espacios_da_400()
    {
        await api.Anonymous().PostJson("/api/v1/auth/register", RegisterBody(NewEmail(), name: "    "))
            .ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ok_con_email_en_otra_capitalizacion()
    {
        var email = NewEmail();
        var registered = await RegisterAsync(email);
        var login = await LoginAsync($" {email.ToUpperInvariant()} ", "contraseña-segura-1").Expect<AuthResponse>(HttpStatusCode.OK);
        Assert.Equal(registered.User.Id, login.User.Id);
        Assert.NotEqual(registered.RefreshToken, login.RefreshToken);
    }

    [Fact]
    public async Task Login_con_email_inexistente_y_con_contrasena_errada_responde_igual()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var wrongPassword = await LoginAsync(email, "otra-contraseña-1").ExpectProblem(HttpStatusCode.Unauthorized, "invalid_credentials");
        var unknownEmail = await LoginAsync(NewEmail(), "otra-contraseña-1").ExpectProblem(HttpStatusCode.Unauthorized, "invalid_credentials");

        Assert.Equal("Correo o contraseña incorrectos.", wrongPassword.GetProperty("title").GetString());
        Assert.Equal(wrongPassword.GetProperty("title").GetString(), unknownEmail.GetProperty("title").GetString());
        Assert.Equal(wrongPassword.GetProperty("code").GetString(), unknownEmail.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_se_bloquea_tras_N_fallos_con_429_account_locked()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        var max = api.Service<IOptions<RateLimitOptions>>().Value.MaxFailedLogins;

        for (var i = 0; i < max; i++)
            await LoginAsync(email, "mala-contraseña-1").ExpectProblem(HttpStatusCode.Unauthorized, "invalid_credentials");

        // Bloqueada: ni con la contraseña correcta, y sin evaluarla (no confirma si era la buena).
        await LoginAsync(email, "contraseña-segura-1").ExpectProblem(HttpStatusCode.TooManyRequests, "account_locked");
    }

    [Fact]
    public async Task Un_correo_inexistente_se_bloquea_igual_que_uno_real()
    {
        var max = api.Service<IOptions<RateLimitOptions>>().Value.MaxFailedLogins;
        var ghost = NewEmail();

        for (var i = 0; i < max; i++)
            await LoginAsync(ghost, "mala-contraseña-1").ExpectProblem(HttpStatusCode.Unauthorized, "invalid_credentials");
        await LoginAsync(ghost, "mala-contraseña-1").ExpectProblem(HttpStatusCode.TooManyRequests, "account_locked");
    }

    [Fact]
    public async Task Un_login_correcto_reinicia_el_contador_de_fallos()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        var max = api.Service<IOptions<RateLimitOptions>>().Value.MaxFailedLogins;

        for (var round = 0; round < 2; round++)
        {
            for (var i = 0; i < max - 1; i++)
                await LoginAsync(email, "mala-contraseña-1").Expect(HttpStatusCode.Unauthorized);
            await LoginAsync(email, "contraseña-segura-1").Expect(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Rate_limit_por_IP_da_429_rate_limited_al_tercer_intento()
    {
        await using var limited = api.Factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimit:AuthPermitPerMinute"] = "2" })));
        var client = limited.CreateClient();
        var email = NewEmail();

        await LoginAsync(email, "cualquier-cosa-123", client).Expect(HttpStatusCode.Unauthorized);
        await LoginAsync(email, "cualquier-cosa-123", client).Expect(HttpStatusCode.Unauthorized);
        await LoginAsync(email, "cualquier-cosa-123", client).ExpectProblem(HttpStatusCode.TooManyRequests, "rate_limited");
    }

    [Fact]
    public async Task Refresh_rota_y_el_token_viejo_ya_no_sirve()
    {
        var first = await RegisterAsync(NewEmail());
        var second = await RefreshAsync(first.RefreshToken).Expect<AuthResponse>(HttpStatusCode.OK);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(first.User.Id, second.User.Id);
        await RefreshAsync(first.RefreshToken).ExpectProblem(HttpStatusCode.Unauthorized, "invalid_refresh_token");
    }

    [Fact]
    public async Task Reuso_de_un_refresh_viejo_revoca_toda_la_familia()
    {
        var first = await RegisterAsync(NewEmail());
        var second = await RefreshAsync(first.RefreshToken).Expect<AuthResponse>(HttpStatusCode.OK);
        var third = await RefreshAsync(second.RefreshToken).Expect<AuthResponse>(HttpStatusCode.OK);

        // Alguien reusa el primero (robo): el vigente también muere.
        await RefreshAsync(first.RefreshToken).ExpectProblem(HttpStatusCode.Unauthorized, "invalid_refresh_token");
        await RefreshAsync(third.RefreshToken).ExpectProblem(HttpStatusCode.Unauthorized, "invalid_refresh_token");
    }

    [Fact]
    public async Task Refresh_concurrente_con_el_mismo_token_tiene_un_solo_ganador()
    {
        var first = await RegisterAsync(NewEmail());
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RefreshAsync(first.RefreshToken)));

        Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized));
        // La rotación atómica garantiza un único ganador aunque lleguen a la vez.
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_desconocido_o_vencido_da_401()
    {
        await RefreshAsync("token-que-no-existe").ExpectProblem(HttpStatusCode.Unauthorized, "invalid_refresh_token");

        var auth = await RegisterAsync(NewEmail());
        await api.WithDbAsync(async db =>
        {
            await db.RefreshTokens.Where(t => t.UserId == auth.User.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct);
        });
        await RefreshAsync(auth.RefreshToken).ExpectProblem(HttpStatusCode.Unauthorized, "invalid_refresh_token");
    }

    [Fact]
    public async Task Logout_revoca_la_familia_y_es_idempotente()
    {
        var first = await RegisterAsync(NewEmail());
        var second = await RefreshAsync(first.RefreshToken).Expect<AuthResponse>(HttpStatusCode.OK);

        await api.Anonymous().PostJson("/api/v1/auth/logout", new { refreshToken = second.RefreshToken }).Expect(HttpStatusCode.NoContent);
        await RefreshAsync(second.RefreshToken).ExpectProblem(HttpStatusCode.Unauthorized, "invalid_refresh_token");
        // El access token de la sesión cerrada tampoco sirve, aunque no haya vencido.
        var client = api.Anonymous();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", second.AccessToken);
        await client.GetAsync("/api/v1/me", Ct).ExpectProblem(HttpStatusCode.Unauthorized, "unauthorized");
        await api.Anonymous().PostJson("/api/v1/auth/logout", new { refreshToken = second.RefreshToken }).Expect(HttpStatusCode.NoContent);
        await api.Anonymous().PostJson("/api/v1/auth/logout", new { refreshToken = "no-existe" }).Expect(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_de_un_usuario_no_afecta_las_sesiones_de_otro()
    {
        var a = await RegisterAsync(NewEmail());
        var b = await RegisterAsync(NewEmail());
        await api.Anonymous().PostJson("/api/v1/auth/logout", new { refreshToken = a.RefreshToken }).Expect(HttpStatusCode.NoContent);
        await RefreshAsync(b.RefreshToken).Expect(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Usuario_baneado_no_puede_login_ni_refresh_y_su_JWT_da_403()
    {
        var email = NewEmail();
        var auth = await RegisterAsync(email);
        await api.WithDbAsync(async db =>
            await db.Users.Where(u => u.Id == auth.User.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Banned), Ct));

        await LoginAsync(email, "contraseña-segura-1").ExpectProblem(HttpStatusCode.Forbidden, "account_banned");
        // Con contraseña errada no se revela el estado de la cuenta.
        await LoginAsync(email, "mala-contraseña-1").ExpectProblem(HttpStatusCode.Unauthorized, "invalid_credentials");
        await RefreshAsync(auth.RefreshToken).ExpectProblem(HttpStatusCode.Forbidden, "account_banned");

        var client = api.Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        await client.GetAsync("/api/v1/me", Ct).ExpectProblem(HttpStatusCode.Forbidden, "account_banned");
    }
}
