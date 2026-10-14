using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Chat;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Tests.Features;

public sealed class ProfileTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly TodayColombia() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

    [Fact]
    public async Task GetMe_devuelve_mi_perfil_con_edad_y_generos_ordenados()
    {
        var u = await api.CreateUserAsync("Lia", c => c.Bio = "hola", "vallenato", "salsa", "reggaeton");
        var me = await u.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);

        Assert.Equal(u.Id, me.Id);
        Assert.Equal("Lia", me.DisplayName);
        Assert.Equal(new[] { "reggaeton", "salsa", "vallenato" }, me.Genres);
        Assert.Equal(User.AgeOn(new DateOnly(1999, 5, 20), TodayColombia()), me.Age);
        Assert.True(me.OnboardingCompleted);
    }

    [Fact]
    public async Task GetMe_sin_token_da_401()
    {
        await api.Anonymous().GetAsync("/api/v1/me", Ct).ExpectProblem(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Patch_actualiza_solo_lo_enviado_y_recorta()
    {
        var u = await api.CreateUserAsync("Lia", c => c.Bio = "bio vieja");
        var me = await u.Client.PatchJson("/api/v1/me", new { displayName = "  Lía Ríos  ", city = " Medellín " })
            .Expect<MeResponse>(HttpStatusCode.OK);

        Assert.Equal("Lía Ríos", me.DisplayName);
        Assert.Equal("Medellín", me.City);
        Assert.Equal("bio vieja", me.Bio);
        Assert.Equal(new DateOnly(1999, 5, 20), me.BirthDate);
        Assert.Equal(new[] { "reggaeton" }, me.Genres);
    }

    [Fact]
    public async Task Patch_con_bio_vacia_la_borra()
    {
        var u = await api.CreateUserAsync(configure: c => c.Bio = "algo");
        var me = await u.Client.PatchJson("/api/v1/me", new { bio = "   " }).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Null(me.Bio);
    }

    [Fact]
    public async Task Patch_reemplaza_la_lista_de_generos_y_acepta_mantener_alguno()
    {
        var u = await api.CreateUserAsync(genres: ["reggaeton", "salsa"]);
        var me = await u.Client.PatchJson("/api/v1/me", new { genres = new[] { "salsa", "vallenato" } }).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Equal(new[] { "salsa", "vallenato" }, me.Genres);
        Assert.Equal(2, await api.WithDbAsync(db => db.UserGenres.CountAsync(g => g.UserId == u.Id, Ct)));
    }

    [Fact]
    public async Task Patch_con_genero_desconocido_da_400_unknown_genre_y_no_cambia_nada()
    {
        var u = await api.CreateUserAsync();
        await u.Client.PatchJson("/api/v1/me", new { genres = new[] { "reggaeton", "no-existe" } })
            .ExpectProblem(HttpStatusCode.BadRequest, "unknown_genre");
        var me = await u.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Equal(new[] { "reggaeton" }, me.Genres);
    }

    [Fact]
    public async Task Patch_con_generos_repetidos_o_vacios_o_de_mas_da_400()
    {
        var u = await api.CreateUserAsync();
        await u.Client.PatchJson("/api/v1/me", new { genres = new[] { "salsa", "SALSA" } }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await u.Client.PatchJson("/api/v1/me", new { genres = Array.Empty<string>() }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await u.Client.PatchJson("/api/v1/me", new { genres = Enumerable.Range(0, 11).Select(i => $"g{i}") }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Patch_birthDate_menor_de_18_da_under_age()
    {
        var u = await api.CreateUserAsync();
        var today = TodayColombia();
        var message = await u.Client.PatchJson("/api/v1/me", new { birthDate = today.AddYears(-18).AddDays(1).ToString("yyyy-MM-dd") })
            .ExpectProblem(HttpStatusCode.BadRequest, "under_age");
        Assert.Equal("Debes tener 18 años o más.", message.GetProperty("title").GetString());

        // Justo 18 años hoy: permitido.
        await u.Client.PatchJson("/api/v1/me", new { birthDate = today.AddYears(-18).ToString("yyyy-MM-dd") }).Expect(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_birthDate_futura_antigua_o_invalida_da_400()
    {
        var u = await api.CreateUserAsync();
        await u.Client.PatchJson("/api/v1/me", new { birthDate = TodayColombia().AddDays(2).ToString("yyyy-MM-dd") }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await u.Client.PatchJson("/api/v1/me", new { birthDate = "1899-12-31" }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await u.Client.PatchJson("/api/v1/me", new { birthDate = "no-es-fecha" }).ExpectProblem(HttpStatusCode.BadRequest);
        await u.Client.PatchJson("/api/v1/me", new { birthDate = "2000-02-30" }).ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_con_textos_invalidos_da_400()
    {
        var u = await api.CreateUserAsync();
        await u.Client.PatchJson("/api/v1/me", new { displayName = "A" }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await u.Client.PatchJson("/api/v1/me", new { displayName = "  " }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await u.Client.PatchJson("/api/v1/me", new { city = new string('x', 81) }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await u.Client.PatchJson("/api/v1/me", new { bio = new string('x', 301) }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task OnboardingCompleted_pasa_a_true_con_fecha_ciudad_y_genero()
    {
        var email = $"{Guid.NewGuid():N}@mail.com";
        var consent = api.Service<IOptions<LegalOptions>>().Value.ConsentVersion;
        var auth = await api.Anonymous().PostJson("/api/v1/auth/register",
            new { email, password = "contraseña-segura-1", displayName = "Nuevo", acceptTerms = true, consentVersion = consent })
            .Expect<AuthResponse>(HttpStatusCode.OK);
        Assert.False(auth.User.OnboardingCompleted);

        var client = api.Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var partial = await client.PatchJson("/api/v1/me", new { birthDate = "1995-03-10", city = "Cali" }).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.False(partial.OnboardingCompleted);

        var done = await client.PatchJson("/api/v1/me", new { genres = new[] { "salsa" } }).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.True(done.OnboardingCompleted);

        var login = await api.Anonymous().PostJson("/api/v1/auth/login", new { email, password = "contraseña-segura-1" }).Expect<AuthResponse>(HttpStatusCode.OK);
        Assert.True(login.User.OnboardingCompleted);
    }

    [Fact]
    public async Task A_no_puede_leer_ni_cambiar_a_B()
    {
        var a = await api.CreateUserAsync("Ana");
        var b = await api.CreateUserAsync("Beto");

        // La identidad sale del JWT: un id en el cuerpo se ignora.
        var changed = await a.Client.PatchJson("/api/v1/me", new { id = b.Id, email = "x@y.com", displayName = "Ana cambiada" }).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Equal(a.Id, changed.Id);

        var meB = await b.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Equal(b.Id, meB.Id);
        Assert.Equal("Beto", meB.DisplayName);
        Assert.Equal(b.Email, meB.Email);

        var meA = await a.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Equal(a.Email, meA.Email);
        Assert.Equal("Ana cambiada", meA.DisplayName);
    }

    [Fact]
    public async Task Consents_acepta_la_version_vigente_y_rechaza_otras()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        var current = api.Service<IOptions<LegalOptions>>().Value.ConsentVersion;

        await a.Client.PostJson("/api/v1/me/consents", new { version = current }).Expect(HttpStatusCode.NoContent);
        await a.Client.PostJson("/api/v1/me/consents", new { version = "1999-01-01" }).ExpectProblem(HttpStatusCode.BadRequest, "consent_required");
        await a.Client.PostJson("/api/v1/me/consents", new { }).ExpectProblem(HttpStatusCode.BadRequest);

        Assert.Equal(1, await api.WithDbAsync(db => db.UserConsents.CountAsync(c => c.UserId == a.Id, Ct)));
        Assert.Equal(0, await api.WithDbAsync(db => db.UserConsents.CountAsync(c => c.UserId == b.Id, Ct)));
        await api.Anonymous().PostJson("/api/v1/me/consents", new { version = current }).ExpectProblem(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Export_incluye_mis_datos_sin_contenido_de_mensajes_y_sin_datos_de_otros()
    {
        var a = await api.CreateUserAsync("Ana");
        var b = await api.CreateUserAsync("Beto");
        var c = await api.CreateUserAsync("Carla");
        var (matchAB, _) = await SeedActivityAsync(a, b);
        await SeedActivityAsync(b, c);

        var export = await a.Client.GetAsync("/api/v1/me/export", Ct).Expect<DataExportResponse>(HttpStatusCode.OK);

        Assert.Equal(a.Id, export.Profile.Id);
        Assert.Single(export.Swipes);
        Assert.Equal("like", export.Swipes[0].Direction);
        var match = Assert.Single(export.Matches);
        Assert.Equal("Beto", match.PeerDisplayName);
        Assert.Equal("active", match.Status);
        Assert.Equal(2, match.MessagesSent);
        Assert.DoesNotContain(export.Matches, m => m.PeerDisplayName == "Carla");

        var raw = await a.Client.GetStringAsync("/api/v1/me/export", Ct);
        Assert.DoesNotContain("ciphertext", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nonce", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(b.Email, raw);
        Assert.NotEqual(Guid.Empty, matchAB);
    }

    [Fact]
    public async Task Delete_me_pide_la_contrasena_y_un_fallo_no_cierra_la_sesion()
    {
        var a = await api.CreateUserAsync();
        await a.Client.DeleteAsync("/api/v1/me", Ct).ExpectProblem(HttpStatusCode.BadRequest);
        await a.Client.DeleteJson("/api/v1/me", new { password = "" }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await a.Client.DeleteJson("/api/v1/me", new { password = "no-es-la-mia-1" }).ExpectProblem(HttpStatusCode.Forbidden, "invalid_password");

        await a.Client.GetAsync("/api/v1/me", Ct).Expect(HttpStatusCode.OK);
        Assert.Equal(1, await api.WithDbAsync(db => db.Users.Where(u => u.Id == a.Id).Select(u => u.FailedLoginAttempts).SingleAsync(Ct)));
        await a.Client.DeleteMe().Expect(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_me_borra_todo_del_usuario_y_su_token_deja_de_servir()
    {
        var a = await api.CreateUserAsync("Ana");
        var b = await api.CreateUserAsync("Beto");
        var (matchId, songId) = await SeedActivityAsync(a, b);
        await api.WithDbAsync(async db =>
        {
            db.RefreshTokens.Add(new RefreshToken { UserId = a.Id, FamilyId = Guid.NewGuid(), TokenHash = Guid.NewGuid().ToByteArray(), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
            db.UserKeys.Add(new UserKey { UserId = a.Id, IdentityKey = new byte[32] });
            await db.SaveChangesAsync(Ct);
        });

        await a.Client.DeleteMe().Expect(HttpStatusCode.NoContent);

        await api.WithDbAsync(async db =>
        {
            Assert.False(await db.Users.AnyAsync(u => u.Id == a.Id, Ct));
            Assert.False(await db.Swipes.AnyAsync(s => s.UserId == a.Id, Ct));
            Assert.False(await db.UserLikes.AnyAsync(l => l.FromUserId == a.Id || l.ToUserId == a.Id, Ct));
            Assert.False(await db.Matches.AnyAsync(m => m.UserAId == a.Id || m.UserBId == a.Id, Ct));
            Assert.False(await db.ChatMessages.AnyAsync(m => m.MatchId == matchId || m.SenderId == a.Id, Ct));
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == a.Id, Ct));
            Assert.False(await db.UserKeys.AnyAsync(k => k.UserId == a.Id, Ct));
            Assert.False(await db.UserConsents.AnyAsync(c => c.UserId == a.Id, Ct));
            Assert.False(await db.UserGenres.AnyAsync(g => g.UserId == a.Id, Ct));
            // B y el catálogo siguen intactos.
            Assert.True(await db.Users.AnyAsync(u => u.Id == b.Id, Ct));
            Assert.True(await db.Songs.AnyAsync(s => s.Id == songId, Ct));
        });

        await a.Client.GetAsync("/api/v1/me", Ct).ExpectProblem(HttpStatusCode.Unauthorized);
        await b.Client.GetAsync("/api/v1/me", Ct).Expect(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_me_de_A_no_toca_a_B()
    {
        var a = await api.CreateUserAsync("Ana");
        var b = await api.CreateUserAsync("Beto");
        var c = await api.CreateUserAsync("Carla");
        await SeedActivityAsync(b, c);

        await a.Client.DeleteMe().Expect(HttpStatusCode.NoContent);

        await api.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.Matches.CountAsync(m => m.UserAId == b.Id || m.UserBId == b.Id, Ct));
            Assert.Equal(3, await db.ChatMessages.CountAsync(m => m.SenderId == b.Id || m.SenderId == c.Id, Ct));
        });
    }

    /// <summary>Crea una canción, un swipe de A, likes recíprocos, un match activo y un mensaje de cada lado (A envía 2).</summary>
    private async Task<(Guid MatchId, Guid SongId)> SeedActivityAsync(TestUser a, TestUser b)
    {
        return await api.WithDbAsync(async db =>
        {
            var genre = await db.Genres.FirstAsync(Ct);
            var song = new Song { Title = $"Canción {Guid.NewGuid():N}", Artist = "Artista", GenreId = genre.Id, CoverUrl = "https://example.test/c.jpg" };
            db.Songs.Add(song);
            db.Swipes.Add(new Swipe { UserId = a.Id, SongId = song.Id, Direction = SwipeDirection.Like });
            db.UserLikes.Add(new UserLike { FromUserId = a.Id, ToUserId = b.Id });
            db.UserLikes.Add(new UserLike { FromUserId = b.Id, ToUserId = a.Id });
            var (x, y) = Match.OrderPair(a.Id, b.Id);
            var match = new Match { UserAId = x, UserBId = y, LastSeq = 3 };
            db.Matches.Add(match);
            await db.SaveChangesAsync(Ct);

            db.ChatMessages.AddRange(
                Message(match.Id, a.Id, 1), Message(match.Id, a.Id, 2), Message(match.Id, b.Id, 3));
            await db.SaveChangesAsync(Ct);
            return (match.Id, song.Id);
        });

        static ChatMessage Message(Guid matchId, Guid senderId, long seq) => new()
        {
            Id = Guid.NewGuid(), MatchId = matchId, SenderId = senderId, Seq = seq, Version = 1,
            SenderBoxKeyId = 1, RecipientBoxKeyId = 1, Nonce = new byte[24], Ciphertext = [1, 2, 3, 4],
        };
    }
}
