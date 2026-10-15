using System.Net;
using System.Net.Http.Headers;
using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Tests.Features;

public sealed class SpotifyAuthTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private FakeSpotify Spotify => api.Spotify;

    private string ConsentVersion => api.Service<IOptions<LegalOptions>>().Value.ConsentVersion;

    private object LoginBody(FakeSpotify.Account account, bool acceptTerms = true)
    {
        var grant = Spotify.Grant(account);
        return new { grant.Code, grant.CodeVerifier, grant.RedirectUri, acceptTerms, consentVersion = acceptTerms ? ConsentVersion : null };
    }

    private Task<HttpResponseMessage> LoginAsync(FakeSpotify.Account account, bool acceptTerms = true)
        => api.Anonymous().PostJson("/api/v1/auth/spotify", LoginBody(account, acceptTerms));

    private static HttpClient WithToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Primer_login_crea_cuenta_sin_contrasena_y_el_segundo_entra_a_la_misma()
    {
        var account = FakeSpotify.NewAccount() with { DisplayName = "  Ana de Spotify  " };

        var first = await LoginAsync(account).Expect<SpotifyAuthResponse>(HttpStatusCode.OK);
        Assert.True(first.IsNewUser);
        Assert.Equal(account.Email, first.User.Email);
        Assert.Equal("Ana de Spotify", first.User.DisplayName);
        Assert.False(first.User.OnboardingCompleted);

        var me = await WithToken(api.Anonymous(), first.AccessToken).GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.True(me.SpotifyLinked);
        Assert.False(me.HasPassword);
        Assert.True(me.ConsentUpToDate);

        // Una cuenta existente entra aunque no mande el consentimiento: se pidió al crearla.
        var second = await LoginAsync(account, acceptTerms: false).Expect<SpotifyAuthResponse>(HttpStatusCode.OK);
        Assert.False(second.IsNewUser);
        Assert.Equal(first.User.Id, second.User.Id);
        await api.Anonymous().PostJson("/api/v1/auth/refresh", new { refreshToken = second.RefreshToken }).Expect(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cuenta_nueva_sin_consentimiento_da_400_y_no_crea_nada()
    {
        var account = FakeSpotify.NewAccount();
        await LoginAsync(account, acceptTerms: false).ExpectProblem(HttpStatusCode.BadRequest, "consent_required");
        Assert.False(await api.WithDbAsync(db => db.Users.AnyAsync(u => u.SpotifyId == account.Id, Ct)));
    }

    [Fact]
    public async Task Correo_de_una_cuenta_existente_da_409_y_nunca_vincula_por_correo()
    {
        var owner = await api.CreateUserAsync();
        var account = FakeSpotify.NewAccount(email: owner.Email.ToUpperInvariant());

        await LoginAsync(account).ExpectProblem(HttpStatusCode.Conflict, "spotify_email_in_use");
        Assert.Null(await api.WithDbAsync(db => db.Users.Where(u => u.Id == owner.Id).Select(u => u.SpotifyId).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Correo_baneado_o_cuenta_suspendida_da_403()
    {
        var banned = FakeSpotify.NewAccount();
        await api.WithDbAsync(async db =>
        {
            db.BannedEmails.Add(new BannedEmail { EmailHash = api.Service<BannedEmailHasher>().Hash(banned.Email!) });
            await db.SaveChangesAsync(Ct);
        });
        await LoginAsync(banned).ExpectProblem(HttpStatusCode.Forbidden, "account_banned");

        var suspended = FakeSpotify.NewAccount();
        await api.CreateUserAsync(configure: u => { u.SpotifyId = suspended.Id; u.Status = UserStatus.Banned; });
        await LoginAsync(suspended).ExpectProblem(HttpStatusCode.Forbidden, "account_banned");
    }

    [Fact]
    public async Task Errores_de_Spotify_se_traducen_a_codigos_estables()
    {
        var account = FakeSpotify.NewAccount();
        var body = LoginBody(account);
        await api.Anonymous().PostJson("/api/v1/auth/spotify", body).Expect(HttpStatusCode.OK);
        // El code es de un solo uso.
        await api.Anonymous().PostJson("/api/v1/auth/spotify", body).ExpectProblem(HttpStatusCode.BadRequest, "spotify_invalid_code");

        await LoginAsync(FakeSpotify.NewAccount() with { MeStatus = HttpStatusCode.Forbidden })
            .ExpectProblem(HttpStatusCode.Forbidden, "spotify_user_not_allowed");
        await LoginAsync(FakeSpotify.NewAccount() with { TokenStatus = HttpStatusCode.ServiceUnavailable })
            .ExpectProblem(HttpStatusCode.BadGateway, "spotify_unavailable");
        await LoginAsync(FakeSpotify.NewAccount() with { MeStatus = HttpStatusCode.InternalServerError })
            .ExpectProblem(HttpStatusCode.BadGateway, "spotify_unavailable");
        await LoginAsync(FakeSpotify.NewAccount() with { Email = null }).ExpectProblem(HttpStatusCode.BadRequest, "spotify_email_missing");
        await LoginAsync(FakeSpotify.NewAccount() with { Email = "no-es-un-correo" }).ExpectProblem(HttpStatusCode.BadRequest, "spotify_email_missing");
    }

    [Fact]
    public async Task Redirect_no_permitida_o_verifier_corto_da_400_sin_llamar_a_Spotify()
    {
        var before = Spotify.Requests;
        await api.Anonymous().PostJson("/api/v1/auth/spotify", new
        {
            code = "x", codeVerifier = FakeSpotify.Verifier, redirectUri = "https://evil.example/cb", acceptTerms = true, consentVersion = ConsentVersion,
        }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await api.Anonymous().PostJson("/api/v1/auth/spotify", new
        {
            code = "x", codeVerifier = "corto", redirectUri = FakeSpotify.RedirectUri, acceptTerms = true, consentVersion = ConsentVersion,
        }).ExpectProblem(HttpStatusCode.BadRequest);
        Assert.Equal(before, Spotify.Requests);
    }

    [Fact]
    public async Task Login_por_correo_de_una_cuenta_sin_contrasena_da_el_401_generico()
    {
        var account = FakeSpotify.NewAccount();
        await LoginAsync(account).Expect(HttpStatusCode.OK);

        var body = await api.Anonymous().PostJson("/api/v1/auth/login", new { email = account.Email, password = "cualquier-cosa-123" })
            .ExpectProblem(HttpStatusCode.Unauthorized, "invalid_credentials");
        Assert.Equal("Correo o contraseña incorrectos.", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Borrar_cuenta_con_Spotify_exige_la_cuenta_vinculada()
    {
        var account = FakeSpotify.NewAccount();
        var auth = await LoginAsync(account).Expect<SpotifyAuthResponse>(HttpStatusCode.OK);
        var client = WithToken(api.Anonymous(), auth.AccessToken);

        await client.DeleteJson("/api/v1/me", new { spotify = Spotify.Grant(FakeSpotify.NewAccount()) })
            .ExpectProblem(HttpStatusCode.Forbidden, "spotify_account_mismatch");
        await client.DeleteJson("/api/v1/me", new { password = "cualquier-cosa-123" }).ExpectProblem(HttpStatusCode.Forbidden, "invalid_password");
        await client.DeleteJson("/api/v1/me", new { }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await client.DeleteJson("/api/v1/me", new { password = "x", spotify = Spotify.Grant(account) })
            .ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");

        await client.DeleteJson("/api/v1/me", new { spotify = Spotify.Grant(account) }).Expect(HttpStatusCode.NoContent);
        Assert.False(await api.WithDbAsync(db => db.Users.AnyAsync(u => u.Id == auth.User.Id, Ct)));
    }

    [Fact]
    public async Task Vincular_permite_entrar_con_Spotify_y_desvincular_lo_quita()
    {
        var user = await api.CreateUserAsync();
        var account = FakeSpotify.NewAccount(email: $"{Guid.NewGuid():N}@otro.test");

        await user.Client.PostJson("/api/v1/me/spotify/link", Spotify.Grant(account)).Expect(HttpStatusCode.NoContent);
        await user.Client.PostJson("/api/v1/me/spotify/link", Spotify.Grant(account)).Expect(HttpStatusCode.NoContent);
        var me = await user.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.True(me.SpotifyLinked);
        Assert.True(me.HasPassword);

        var login = await LoginAsync(account, acceptTerms: false).Expect<SpotifyAuthResponse>(HttpStatusCode.OK);
        Assert.False(login.IsNewUser);
        Assert.Equal(user.Id, login.User.Id);
        // El correo de Spotify no reemplaza el de la cuenta.
        Assert.Equal(user.Email, login.User.Email);

        // Otra cuenta de Spotify, o el mismo Spotify en otro perfil, chocan.
        await user.Client.PostJson("/api/v1/me/spotify/link", Spotify.Grant(FakeSpotify.NewAccount()))
            .ExpectProblem(HttpStatusCode.Conflict, "spotify_already_linked");
        var other = await api.CreateUserAsync();
        await other.Client.PostJson("/api/v1/me/spotify/link", Spotify.Grant(account)).ExpectProblem(HttpStatusCode.Conflict, "spotify_already_linked");

        await user.Client.DeleteAsync("/api/v1/me/spotify/link", Ct).Expect(HttpStatusCode.NoContent);
        await user.Client.DeleteAsync("/api/v1/me/spotify/link", Ct).Expect(HttpStatusCode.NoContent);
        Assert.False((await user.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK)).SpotifyLinked);
        // Ya desvinculado, el mismo Spotify intentaría crear cuenta con un correo nuevo: no vuelve a entrar al perfil.
        var fresh = await LoginAsync(account).Expect<SpotifyAuthResponse>(HttpStatusCode.OK);
        Assert.NotEqual(user.Id, fresh.User.Id);
    }

    [Fact]
    public async Task Cuenta_sin_contrasena_no_puede_desvincular_Spotify()
    {
        var auth = await LoginAsync(FakeSpotify.NewAccount()).Expect<SpotifyAuthResponse>(HttpStatusCode.OK);
        await WithToken(api.Anonymous(), auth.AccessToken).DeleteAsync("/api/v1/me/spotify/link", Ct)
            .ExpectProblem(HttpStatusCode.Conflict, "spotify_last_login_method");
    }
}
