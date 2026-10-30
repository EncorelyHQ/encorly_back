using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Chat;
using Encorely.Api.Features.Events;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Encorely.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Net.Http.Json;
using Match = Encorely.Api.Features.Matching.Match;
using TestFactory = Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>;

namespace Encorely.Tests.Features;

/// <summary>Regresiones de la ronda de endurecimiento (v1.0.0-draft.5): cada test falla sin su fix.</summary>
public sealed class SecurityHardeningTests(ApiFixture api)
{
    private const string Password = "contraseña-segura-1";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string ConsentVersion => api.Service<IOptions<LegalOptions>>().Value.ConsentVersion;

    private static string NewEmail() => $"{Guid.NewGuid():N}@mail.com";

    private Task<HttpResponseMessage> RegisterRawAsync(string email)
        => api.Anonymous().PostJson("/api/v1/auth/register", new { email, password = Password, displayName = "Ana", acceptTerms = true, consentVersion = ConsentVersion });

    private async Task<(AuthResponse Auth, HttpClient Client, string Email)> RegisterAsync()
    {
        var email = NewEmail();
        var auth = await RegisterRawAsync(email).Expect<AuthResponse>(HttpStatusCode.OK);
        return (auth, ClientFor(api.Factory, auth.AccessToken), email);
    }

    private static HttpClient ClientFor(TestFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<TestUser> AdminAsync() => api.CreateUserAsync("Admin", u => u.Role = UserRole.Admin);

    private static Guid SidOf(string jwt)
        => new JsonWebToken(jwt).TryGetPayloadValue<string>("sid", out var sid) ? Guid.Parse(sid) : throw new InvalidOperationException("El access token no trae sid.");

    // ---------- 1. Evasión de ban ----------

    [Fact]
    public async Task Baneado_que_borra_su_cuenta_no_puede_volver_a_registrarse_con_el_mismo_correo()
    {
        var admin = await AdminAsync();
        var (auth, _, email) = await RegisterAsync();
        await admin.Client.PutJson($"/api/v1/admin/users/{auth.User.Id}/status", new { status = "banned" }).Expect(HttpStatusCode.NoContent);

        // Con la cuenta aún viva el registro responde 409 como siempre (no revela el ban).
        await RegisterRawAsync(email).ExpectProblem(HttpStatusCode.Conflict, "email_taken");

        // El borrado sigue permitido (Ley 1581)...
        await api.Anonymous().PostJson("/api/v1/account-deletion", new { email, password = Password }).Expect(HttpStatusCode.NoContent);
        Assert.False(await api.WithDbAsync(db => db.Users.AnyAsync(u => u.Id == auth.User.Id, Ct)));

        // ...pero el correo queda vetado, también con otras mayúsculas.
        var body = await RegisterRawAsync(email.ToUpperInvariant()).ExpectProblem(HttpStatusCode.Forbidden, "account_banned");
        Assert.Equal("No puedes crear una cuenta con este correo.", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Ban_es_idempotente_y_reactivar_levanta_el_veto_de_correo()
    {
        var admin = await AdminAsync();
        var user = await api.CreateUserAsync();
        var hash = api.Service<BannedEmailHasher>().Hash(user.Email);
        Task<bool> Vetoed() => api.WithDbAsync(db => db.BannedEmails.AnyAsync(b => b.EmailHash == hash, Ct));

        await admin.Client.PutJson($"/api/v1/admin/users/{user.Id}/status", new { status = "banned" }).Expect(HttpStatusCode.NoContent);
        await admin.Client.PutJson($"/api/v1/admin/users/{user.Id}/status", new { status = "banned" }).Expect(HttpStatusCode.NoContent);
        Assert.True(await Vetoed());
        Assert.Equal(1, await api.WithDbAsync(db => db.BannedEmails.CountAsync(b => b.EmailHash == hash, Ct)));

        await admin.Client.PutJson($"/api/v1/admin/users/{user.Id}/status", new { status = "active" }).Expect(HttpStatusCode.NoContent);
        Assert.False(await Vetoed());
    }

    [Fact]
    public async Task El_veto_guarda_un_hmac_y_respeta_los_vetos_viejos_en_sha256()
    {
        var admin = await AdminAsync();
        var user = await api.CreateUserAsync();
        var legacy = BannedEmailHasher.LegacyHash(user.Email);
        await admin.Client.PutJson($"/api/v1/admin/users/{user.Id}/status", new { status = "banned" }).Expect(HttpStatusCode.NoContent);
        // Sin la llave, el hash de la tabla no se puede confirmar por diccionario.
        Assert.False(await api.WithDbAsync(db => db.BannedEmails.AnyAsync(b => b.EmailHash == legacy, Ct)));

        // Un veto anterior a la llave (SHA-256 a secas) sigue bloqueando el registro y se levanta al reactivar.
        var old = $"{Guid.NewGuid():N}@encorely.test";
        await api.WithDbAsync(async db =>
        {
            db.BannedEmails.Add(new BannedEmail { EmailHash = BannedEmailHasher.LegacyHash(old) });
            await db.SaveChangesAsync(Ct);
        });
        await RegisterRawAsync(old).ExpectProblem(HttpStatusCode.Forbidden, "account_banned");

        await api.WithDbAsync(async db =>
        {
            db.BannedEmails.Add(new BannedEmail { EmailHash = legacy });
            await db.SaveChangesAsync(Ct);
        });
        await admin.Client.PutJson($"/api/v1/admin/users/{user.Id}/status", new { status = "active" }).Expect(HttpStatusCode.NoContent);
        Assert.False(await api.WithDbAsync(db => db.BannedEmails.AnyAsync(b => b.EmailHash == legacy, Ct)));
    }

    [Fact]
    public async Task La_api_responde_con_cache_control_no_store()
    {
        var user = await api.CreateUserAsync();
        var me = await user.Client.GetAsync("/api/v1/me", Ct).Expect(HttpStatusCode.OK);
        Assert.True(me.Headers.CacheControl?.NoStore);
        var anon = await api.Anonymous().GetAsync("/api/v1/me", Ct);
        Assert.True(anon.Headers.CacheControl?.NoStore);
    }

    // ---------- 2 y 3. Bloqueo por intentos ----------

    [Fact]
    public async Task Treinta_logins_fallidos_en_paralelo_bloquean_la_cuenta()
    {
        var (_, _, email) = await RegisterAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 30)
            .Select(_ => api.Anonymous().PostJson("/api/v1/auth/login", new { email, password = "contraseña-incorrecta-1" })));
        Assert.DoesNotContain(results, r => r.StatusCode == HttpStatusCode.InternalServerError);

        await api.Anonymous().PostJson("/api/v1/auth/login", new { email, password = Password })
            .ExpectProblem(HttpStatusCode.TooManyRequests, "account_locked");
    }

    [Fact]
    public async Task Borrado_web_aplica_el_bloqueo_por_intentos_y_no_borra_con_la_cuenta_bloqueada()
    {
        var (auth, _, email) = await RegisterAsync();
        var max = api.Service<IOptions<RateLimitOptions>>().Value.MaxFailedLogins;

        for (var i = 0; i < max; i++)
            await api.Anonymous().PostJson("/api/v1/account-deletion", new { email, password = "contraseña-incorrecta-1" })
                .ExpectProblem(HttpStatusCode.Unauthorized, "invalid_credentials");

        await api.Anonymous().PostJson("/api/v1/account-deletion", new { email, password = Password })
            .ExpectProblem(HttpStatusCode.TooManyRequests, "account_locked");
        Assert.True(await api.WithDbAsync(db => db.Users.AnyAsync(u => u.Id == auth.User.Id, Ct)));
    }

    // ---------- 4. Rate limit por IP detrás de proxy ----------

    private TestFactory Factory(Dictionary<string, string?> config, Action<IWebHostBuilder>? more = null)
        => api.Factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(config));
            more?.Invoke(b);
        });

    private static async Task<List<HttpStatusCode>> LoginsWithForwardedForAsync(HttpClient client, int count)
    {
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < count; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new { email = NewEmail(), password = "x" }, options: Http.Json),
            };
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{i + 1}");
            codes.Add((await client.SendAsync(request, Ct)).StatusCode);
        }
        return codes;
    }

    [Fact]
    public async Task Sin_redes_de_confianza_X_Forwarded_For_se_ignora_y_no_evita_el_rate_limit()
    {
        await using var factory = Factory(new() { ["RateLimit:AuthPermitPerMinute"] = "2" });

        var codes = await LoginsWithForwardedForAsync(factory.CreateClient(), 3);

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], codes);
    }

    [Fact]
    public async Task Con_proxy_de_confianza_cada_IP_forwarded_tiene_su_propio_cupo_y_un_origen_ajeno_se_ignora()
    {
        var config = new Dictionary<string, string?> { ["RateLimit:AuthPermitPerMinute"] = "2", ["Proxy:TrustedNetworks"] = "10.0.0.0/8" };

        await using var trusted = Factory(config, b => b.ConfigureTestServices(s => s.AddSingleton<IStartupFilter>(new RemoteIpFilter("10.1.2.3"))));
        Assert.All(await LoginsWithForwardedForAsync(trusted.CreateClient(), 3), c => Assert.Equal(HttpStatusCode.Unauthorized, c));

        await using var untrusted = Factory(config, b => b.ConfigureTestServices(s => s.AddSingleton<IStartupFilter>(new RemoteIpFilter("192.168.1.9"))));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginsWithForwardedForAsync(untrusted.CreateClient(), 3))[2]);
    }

    [Fact]
    public async Task El_bloqueo_por_intentos_es_por_IP_y_un_tercero_no_bloquea_a_la_victima()
    {
        var (_, _, email) = await RegisterAsync();
        var max = api.Service<IOptions<RateLimitOptions>>().Value.MaxFailedLogins;
        var config = new Dictionary<string, string?> { ["Proxy:TrustedNetworks"] = "10.0.0.0/8" };
        await using var behindProxy = Factory(config, b => b.ConfigureTestServices(s => s.AddSingleton<IStartupFilter>(new RemoteIpFilter("10.1.2.3"))));
        var client = behindProxy.CreateClient();

        Task<HttpResponseMessage> LoginFrom(string ip, string password)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new { email, password }, options: Http.Json),
            };
            request.Headers.Add("X-Forwarded-For", ip);
            return client.SendAsync(request, Ct);
        }

        for (var i = 0; i < max; i++)
            await LoginFrom("203.0.113.66", "contraseña-incorrecta-1").ExpectProblem(HttpStatusCode.Unauthorized, "invalid_credentials");
        await LoginFrom("203.0.113.66", Password).ExpectProblem(HttpStatusCode.TooManyRequests, "account_locked");

        // La víctima, desde su IP, sigue entrando.
        await LoginFrom("198.51.100.7", Password).Expect(HttpStatusCode.OK);
    }

    private sealed class RemoteIpFilter(string ip) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((ctx, n) =>
            {
                ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
                return n();
            });
            next(app);
        };
    }

    // ---------- 5. Carácter NUL ----------

    [Fact]
    public async Task Login_con_NUL_en_el_correo_da_400()
        => await api.Anonymous().PostJson("/api/v1/auth/login", new { email = "ana\0@mail.com", password = Password }).ExpectProblem(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Patch_me_con_NUL_en_el_nombre_da_400()
    {
        var user = await api.CreateUserAsync();
        await user.Client.PatchJson("/api/v1/me", new { displayName = "Ana\0" }).ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Query_string_con_NUL_nunca_da_500()
    {
        var admin = await AdminAsync();
        var city = await admin.Client.GetAsync("/api/v1/events?city=%00", Ct);
        Assert.Contains(city.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest });

        // Este sí llega a Postgres (ILIKE): la red de seguridad del handler lo convierte en 400.
        await admin.Client.GetAsync("/api/v1/admin/songs?query=a%00b", Ct).ExpectProblem(HttpStatusCode.BadRequest, "invalid_body");
    }

    [Fact]
    public async Task Busqueda_itunes_con_NUL_da_400_sin_llamar_a_la_red()
    {
        var calls = 0;
        await using var factory = api.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddHttpClient<Encorely.Api.Features.Admin.ITunesClient>(c => c.BaseAddress = new Uri(Encorely.Api.Features.Admin.ITunesClient.BaseAddress))
                .ConfigurePrimaryHttpMessageHandler(() => new CountingHandler(() => calls++))));
        var admin = await AdminAsync();
        var client = ClientFor(factory, admin.AccessToken);

        await client.GetAsync("/api/v1/admin/itunes/search?term=ka%00li", Ct).ExpectProblem(HttpStatusCode.BadRequest);

        Assert.Equal(0, calls);
    }

    private sealed class CountingHandler(Action onCall) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            onCall();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        }
    }

    // ---------- 6. null dentro de arrays ----------

    [Fact]
    public async Task Null_dentro_de_arrays_da_400_en_cada_contrato()
    {
        var user = await api.CreateUserAsync();
        var admin = await AdminAsync();

        await user.Client.PatchJson("/api/v1/me", new { genres = new string?[] { null } }).ExpectProblem(HttpStatusCode.BadRequest);
        await user.Client.PostJson("/api/v1/me/spotify/import", new { topArtists = new object?[] { null } }).ExpectProblem(HttpStatusCode.BadRequest);
        await user.Client.PostJson("/api/v1/me/spotify/import", new { topArtists = new[] { new { name = "x", genres = new string?[] { null } } } })
            .ExpectProblem(HttpStatusCode.BadRequest);
        await user.Client.PostJson("/api/v1/reports", new { userId = Guid.NewGuid(), reason = "spam", evidence = new object?[] { null } })
            .ExpectProblem(HttpStatusCode.BadRequest);
        await admin.Client.PostJson("/api/v1/admin/events", new
        {
            name = "Festival nulo", artistLineup = new string?[] { "Uno", null }, venue = "Arena", city = "Bogotá", startsAt = DateTimeOffset.UtcNow.AddDays(5),
        }).ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Null_en_arrays_reporta_el_mensaje_en_espanol_por_campo()
    {
        var user = await api.CreateUserAsync();
        var body = await user.Client.PatchJson("/api/v1/me", new { genres = new string?[] { null } }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal("No puede contener elementos vacíos.", body.GetProperty("errors").GetProperty("genres")[0].GetString());
    }

    // ---------- 7. Rotación concurrente de llaves ----------

    [Fact]
    public async Task Rotaciones_concurrentes_nunca_hacen_retroceder_la_llave_vigente()
    {
        for (var round = 0; round < 5; round++)
        {
            var user = await api.CreateUserAsync();
            await ChatKit.PutKeysAsync(user, 1);

            var results = await Task.WhenAll(Enumerable.Range(2, 10)
                .Select(id => user.Client.PutJson("/api/v1/keys/me", ChatKit.KeysBody(id))));

            Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, $"Llegó {(int)r.StatusCode}"));
            Assert.Equal(11, await api.WithDbAsync(db => db.UserKeys.Where(k => k.UserId == user.Id).Select(k => k.CurrentBoxKeyId).SingleAsync(Ct)));
        }
    }

    [Fact]
    public async Task AdvisoryLock_exige_transaccion_abierta()
    {
        await api.WithDbAsync(async db =>
            await Assert.ThrowsAsync<InvalidOperationException>(() => AdvisoryLock.AcquireAsync(db, "x", Guid.NewGuid(), Ct)));
        Assert.NotEqual(AdvisoryLock.Key("a", Guid.Empty), AdvisoryLock.Key("b", Guid.Empty));
        Assert.Equal(AdvisoryLock.Key("a", Guid.Empty), AdvisoryLock.Key("a", Guid.Empty));
    }

    // ---------- 8. Franking sobrevive al ack ----------

    private static string Envelope(Guid messageId, Guid matchId, Guid sender, Guid recipient, string body)
        => JsonSerializer.Serialize(new { v = 1, id = messageId, m = matchId, s = sender, r = recipient, t = 1760000000000L, k = "text", b = body });

    private static byte[] Tag(byte[] key, Guid messageId, string plaintext)
    {
        var data = Encoding.UTF8.GetBytes("ENCORELY-FRANK-v1").Concat(messageId.ToByteArray(bigEndian: true)).Concat(Encoding.UTF8.GetBytes(plaintext)).ToArray();
        return HMACSHA256.HashData(key, data);
    }

    [Fact]
    public async Task Reporte_con_franking_sigue_verificando_despues_del_ack_del_receptor()
    {
        var (a, b, matchId) = await ChatKit.PairAsync(api);
        var key = RandomNumberGenerator.GetBytes(32);
        var id = Guid.NewGuid();
        var plaintext = Envelope(id, matchId, b.Id, a.Id, "mensaje ofensivo");

        await ChatKit.SendAsync(b, matchId, ChatKit.MessageBody(id, 1, 1, franking: Tag(key, id, plaintext))).Expect(HttpStatusCode.Created);
        await a.Client.PostJson($"/api/v1/matches/{matchId}/messages/ack", new { upToSeq = 1 }).Expect(HttpStatusCode.NoContent);
        Assert.False(await api.WithDbAsync(db => db.ChatMessages.AnyAsync(m => m.Id == id, Ct)));

        var created = await a.Client.PostJson("/api/v1/reports", new
        {
            userId = b.Id, reason = "harassment", matchId,
            evidence = new[] { new { messageId = id, plaintext, frankingKey = key } },
        }).Expect<ReportCreatedDto>(HttpStatusCode.Accepted);

        var stored = await api.WithDbAsync(db => db.Reports.AsNoTracking().SingleAsync(r => r.Id == created.Id, Ct));
        Assert.True(Assert.Single(stored.Evidence).Verified);
    }

    [Fact]
    public async Task Reusar_el_id_de_un_mensaje_ya_confirmado_no_crea_un_duplicado()
    {
        var (a, b, matchId) = await ChatKit.PairAsync(api);
        var key = RandomNumberGenerator.GetBytes(32);
        var id = Guid.NewGuid();
        var first = Tag(key, id, Envelope(id, matchId, b.Id, a.Id, "hola"));
        await ChatKit.SendAsync(b, matchId, ChatKit.MessageBody(id, 1, 1, franking: first)).Expect(HttpStatusCode.Created);
        await a.Client.PostJson($"/api/v1/matches/{matchId}/messages/ack", new { upToSeq = 1 }).Expect(HttpStatusCode.NoContent);

        // Reintento del mismo mensaje: ya entregado, no se duplica.
        await ChatKit.SendAsync(b, matchId, ChatKit.MessageBody(id, 1, 1, franking: first))
            .ExpectProblem(HttpStatusCode.Conflict, "message_already_delivered");
        // Contenido nuevo con el id viejo (cliente modificado): conflicto, con tag nuevo o sin tag.
        var other = Tag(key, id, Envelope(id, matchId, b.Id, a.Id, "mensaje ofensivo"));
        await ChatKit.SendAsync(b, matchId, ChatKit.MessageBody(id, 1, 1, franking: other)).ExpectProblem(HttpStatusCode.Conflict, "message_id_conflict");
        await ChatKit.SendAsync(b, matchId, ChatKit.MessageBody(id, 1, 1)).ExpectProblem(HttpStatusCode.Conflict, "message_id_conflict");
        // Ni el otro lado del match puede ocupar ese id.
        await ChatKit.SendAsync(a, matchId, ChatKit.MessageBody(id, 1, 1, franking: first)).ExpectProblem(HttpStatusCode.Conflict, "message_id_conflict");

        await api.WithDbAsync(async db =>
        {
            Assert.False(await db.ChatMessages.AnyAsync(m => m.Id == id, Ct));
            Assert.Equal(1, await db.Matches.Where(m => m.Id == matchId).Select(m => m.LastSeq).SingleAsync(Ct));
        });
    }

    [Fact]
    public async Task Reusar_el_id_de_un_mensaje_sin_franking_ya_confirmado_tampoco_crea_un_duplicado()
    {
        var (a, b, matchId) = await ChatKit.PairAsync(api);
        var id = Guid.NewGuid();
        await ChatKit.SendAsync(b, matchId, ChatKit.MessageBody(id, 1, 1)).Expect(HttpStatusCode.Created);
        await a.Client.PostJson($"/api/v1/matches/{matchId}/messages/ack", new { upToSeq = 1 }).Expect(HttpStatusCode.NoContent);

        await ChatKit.SendAsync(b, matchId, ChatKit.MessageBody(id, 1, 1)).ExpectProblem(HttpStatusCode.Conflict, "message_already_delivered");
        var key = RandomNumberGenerator.GetBytes(32);
        var tagged = Tag(key, id, Envelope(id, matchId, b.Id, a.Id, "otro"));
        await ChatKit.SendAsync(b, matchId, ChatKit.MessageBody(id, 1, 1, franking: tagged)).ExpectProblem(HttpStatusCode.Conflict, "message_id_conflict");
        await ChatKit.SendAsync(a, matchId, ChatKit.MessageBody(id, 1, 1)).ExpectProblem(HttpStatusCode.Conflict, "message_id_conflict");

        await api.WithDbAsync(async db =>
        {
            Assert.True(await db.DeliveredMessageIds.AnyAsync(d => d.MessageId == id, Ct));
            Assert.Equal(1, await db.Matches.Where(m => m.Id == matchId).Select(m => m.LastSeq).SingleAsync(Ct));
        });
    }

    [Fact]
    public async Task Evidencia_de_mensaje_borrado_solo_vale_para_quien_la_recibio_y_de_quien_la_envio()
    {
        var (a, b, matchId) = await ChatKit.PairAsync(api);
        var key = RandomNumberGenerator.GetBytes(32);
        var id = Guid.NewGuid();
        var plaintext = Envelope(id, matchId, b.Id, a.Id, "hola");
        await ChatKit.SendAsync(b, matchId, ChatKit.MessageBody(id, 1, 1, franking: Tag(key, id, plaintext))).Expect(HttpStatusCode.Created);
        await a.Client.PostJson($"/api/v1/matches/{matchId}/messages/ack", new { upToSeq = 1 }).Expect(HttpStatusCode.NoContent);

        // B (el remitente) no puede usar su propio mensaje como evidencia contra A.
        await b.Client.PostJson("/api/v1/reports", new { userId = a.Id, reason = "spam", evidence = new[] { new { messageId = id, plaintext, frankingKey = key } } })
            .ExpectProblem(HttpStatusCode.BadRequest, "invalid_evidence");

        // Un mensaje sin franking que ya se borró no es evidencia.
        var plainId = Guid.NewGuid();
        await ChatKit.SendAsync(b, matchId, ChatKit.MessageBody(plainId, 1, 1)).Expect(HttpStatusCode.Created);
        await a.Client.PostJson($"/api/v1/matches/{matchId}/messages/ack", new { upToSeq = 2 }).Expect(HttpStatusCode.NoContent);
        await a.Client.PostJson("/api/v1/reports", new { userId = b.Id, reason = "spam", evidence = new[] { new { messageId = plainId, plaintext = "x", frankingKey = key } } })
            .ExpectProblem(HttpStatusCode.BadRequest, "invalid_evidence");
    }

    [Fact]
    public async Task Mantenimiento_borra_compromisos_de_franking_de_mas_de_180_dias()
    {
        var (a, b, matchId) = await ChatKit.PairAsync(api);
        var oldId = Guid.NewGuid();
        var freshId = Guid.NewGuid();
        await api.WithDbAsync(async db =>
        {
            db.MessageFrankingRecords.Add(new MessageFrankingRecord
            {
                MessageId = oldId, MatchId = matchId, SenderId = b.Id, RecipientId = a.Id, FrankingTag = RandomNumberGenerator.GetBytes(32),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-181),
            });
            db.MessageFrankingRecords.Add(new MessageFrankingRecord
            {
                MessageId = freshId, MatchId = matchId, SenderId = b.Id, RecipientId = a.Id, FrankingTag = RandomNumberGenerator.GetBytes(32),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-179),
            });
            await db.SaveChangesAsync(Ct);
        });

        await api.Service<MaintenanceService>().RunOnceAsync(Ct);

        Assert.False(await api.WithDbAsync(db => db.MessageFrankingRecords.AnyAsync(f => f.MessageId == oldId, Ct)));
        Assert.True(await api.WithDbAsync(db => db.MessageFrankingRecords.AnyAsync(f => f.MessageId == freshId, Ct)));
    }

    // ---------- 9. PATCH /me concurrente ----------

    [Fact]
    public async Task Patch_me_concurrente_con_generos_no_da_500_y_deja_uno_de_los_enviados()
    {
        var user = await api.CreateUserAsync(null, null, "reggaeton");
        string[][] sets = [["salsa", "cumbia"], ["bachata", "vallenato"], ["salsa", "bachata"], ["cumbia", "vallenato"], ["champeta", "salsa"],
            ["bachata", "cumbia"], ["vallenato", "salsa"], ["champeta", "cumbia"]];

        var results = await Task.WhenAll(sets.Select(genres => user.Client.PatchJson("/api/v1/me", new { genres })));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var me = await user.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Contains(sets, s => s.Order().SequenceEqual(me.Genres));
    }

    // ---------- 10. Importación Spotify concurrente ----------

    [Fact]
    public async Task Importaciones_spotify_concurrentes_no_suman_mas_del_tope()
    {
        for (var round = 0; round < 10; round++)
        {
            var user = await api.CreateUserAsync(null, null, "bachata");
            object Import(string genre) => new { topArtists = Enumerable.Range(0, 50).Select(i => new { name = $"Artista {i}", genres = new[] { genre } }).ToArray() };

            var results = await Task.WhenAll(user.Client.PostJson("/api/v1/me/spotify/import", Import("reggaeton")),
                user.Client.PostJson("/api/v1/me/spotify/import", Import("salsa")));
            Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

            var total = await api.WithDbAsync(db => db.UserGenreScores.Where(s => s.UserId == user.Id).SumAsync(s => s.ImportedWeight, Ct));
            Assert.True(total <= 10 + 1e-6, $"Ronda {round}: el aporte sumó {total}");
        }
    }

    // ---------- 11. Ban concurrente con likes ----------

    [Fact]
    public async Task Ban_concurrente_con_likes_reciprocos_nunca_deja_un_match_activo_con_el_baneado()
    {
        var admin = await AdminAsync();
        for (var round = 0; round < 4; round++)
        {
            var target = await api.CreateUserAsync("Objetivo");
            var others = new List<TestUser>();
            for (var i = 0; i < 5; i++) others.Add(await api.CreateUserAsync($"Otro{i}"));
            var ev = new Event { Name = "Festival de pruebas", Venue = "Arena", City = "Bogotá", StartsAt = DateTimeOffset.UtcNow.AddDays(10) };
            await api.WithDbAsync(async db =>
            {
                db.Events.Add(ev);
                foreach (var u in others.Append(target)) db.EventAttendances.Add(new EventAttendance { EventId = ev.Id, UserId = u.Id });
                await db.SaveChangesAsync(Ct);
            });
            foreach (var o in others) await o.Client.PostJson("/api/v1/likes", new { targetUserId = target.Id, eventId = ev.Id }).Expect(HttpStatusCode.OK);

            var calls = others.Select(o => target.Client.PostJson("/api/v1/likes", new { targetUserId = o.Id, eventId = ev.Id }))
                .Append(admin.Client.PutJson($"/api/v1/admin/users/{target.Id}/status", new { status = "banned" }));
            var results = await Task.WhenAll(calls);

            Assert.DoesNotContain(results, r => r.StatusCode == HttpStatusCode.InternalServerError);
            Assert.Equal(0, await api.WithDbAsync(db => db.Matches.CountAsync(m => m.Status == MatchStatus.Active && (m.UserAId == target.Id || m.UserBId == target.Id), Ct)));
        }
    }

    // ---------- 12. Push tokens ligados a la sesión ----------

    private static string PushToken() => $"ExponentPushToken[{Guid.NewGuid():N}]";

    private Task<DevicePushTokenRow?> RowOf(string token) => api.WithDbAsync(async db =>
        await db.DevicePushTokens.AsNoTracking().Where(t => t.Token == token).Select(t => new DevicePushTokenRow(t.UserId, t.SessionId)).SingleOrDefaultAsync(Ct));

    private sealed record DevicePushTokenRow(Guid UserId, Guid? SessionId);

    private async Task<(AuthResponse Auth, HttpClient Client, string Token)> SessionWithPushAsync(string email)
    {
        var auth = await api.Anonymous().PostJson("/api/v1/auth/login", new { email, password = Password }).Expect<AuthResponse>(HttpStatusCode.OK);
        var client = ClientFor(api.Factory, auth.AccessToken);
        var token = PushToken();
        await client.PutJson("/api/v1/devices/push-token", new { token, platform = "android" }).Expect(HttpStatusCode.NoContent);
        return (auth, client, token);
    }

    [Fact]
    public async Task Access_token_lleva_el_sid_de_la_familia_y_el_push_token_lo_guarda()
    {
        var (auth, client, email) = await RegisterAsync();
        var sid = SidOf(auth.AccessToken);
        var family = await api.WithDbAsync(db => db.RefreshTokens.Where(t => t.UserId == auth.User.Id).Select(t => t.FamilyId).SingleAsync(Ct));
        Assert.Equal(family, sid);

        var token = PushToken();
        await client.PutJson("/api/v1/devices/push-token", new { token, platform = "ios" }).Expect(HttpStatusCode.NoContent);
        Assert.Equal(sid, (await RowOf(token))!.SessionId);

        // Rotar el refresh conserva la sesión.
        var refreshed = await api.Anonymous().PostJson("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken }).Expect<AuthResponse>(HttpStatusCode.OK);
        Assert.Equal(sid, SidOf(refreshed.AccessToken));

        var login = await api.Anonymous().PostJson("/api/v1/auth/login", new { email, password = Password }).Expect<AuthResponse>(HttpStatusCode.OK);
        Assert.NotEqual(sid, SidOf(login.AccessToken));
    }

    [Fact]
    public async Task Logout_borra_los_push_tokens_de_esa_sesion_y_no_los_de_otra()
    {
        var (_, _, email) = await RegisterAsync();
        var one = await SessionWithPushAsync(email);
        var two = await SessionWithPushAsync(email);

        await api.Anonymous().PostJson("/api/v1/auth/logout", new { refreshToken = one.Auth.RefreshToken }).Expect(HttpStatusCode.NoContent);

        Assert.Null(await RowOf(one.Token));
        Assert.NotNull(await RowOf(two.Token));
    }

    [Fact]
    public async Task Cerrar_una_sesion_borra_solo_sus_push_tokens()
    {
        var (_, _, email) = await RegisterAsync();
        var one = await SessionWithPushAsync(email);
        var two = await SessionWithPushAsync(email);

        await two.Client.DeleteAsync($"/api/v1/me/sessions/{SidOf(one.Auth.AccessToken)}", Ct).Expect(HttpStatusCode.NoContent);

        Assert.Null(await RowOf(one.Token));
        Assert.NotNull(await RowOf(two.Token));
    }

    [Fact]
    public async Task Cerrar_todas_las_sesiones_borra_todos_los_push_tokens_del_usuario()
    {
        var (auth, _, email) = await RegisterAsync();
        var one = await SessionWithPushAsync(email);
        var two = await SessionWithPushAsync(email);
        var legacy = PushToken();
        await api.WithDbAsync(async db =>
        {
            db.DevicePushTokens.Add(new Encorely.Api.Features.Devices.DevicePushToken { UserId = auth.User.Id, Token = legacy });
            await db.SaveChangesAsync(Ct);
        });

        await two.Client.DeleteAsync("/api/v1/me/sessions", Ct).Expect(HttpStatusCode.NoContent);

        Assert.Null(await RowOf(one.Token));
        Assert.Null(await RowOf(two.Token));
        Assert.Null(await RowOf(legacy));
    }

    [Fact]
    public async Task Ban_borra_todos_los_push_tokens_del_usuario()
    {
        var admin = await AdminAsync();
        var (auth, _, email) = await RegisterAsync();
        var session = await SessionWithPushAsync(email);

        await admin.Client.PutJson($"/api/v1/admin/users/{auth.User.Id}/status", new { status = "banned" }).Expect(HttpStatusCode.NoContent);

        Assert.Null(await RowOf(session.Token));
    }

    [Fact]
    public async Task Un_access_token_de_una_sesion_cerrada_no_puede_registrar_push_tokens()
    {
        var (_, _, email) = await RegisterAsync();
        var session = await SessionWithPushAsync(email);
        await api.Anonymous().PostJson("/api/v1/auth/logout", new { refreshToken = session.Auth.RefreshToken }).Expect(HttpStatusCode.NoContent);

        var token = PushToken();
        await session.Client.PutJson("/api/v1/devices/push-token", new { token, platform = "android" }).ExpectProblem(HttpStatusCode.Unauthorized);
        Assert.Null(await RowOf(token));
    }

    // ---------- 14 y 16. Entorno de producción ----------

    private TestFactory Production(string sslMode, string proxy = "10.0.0.0/8", string publicBaseUrl = "https://api.encorely.test")
        => api.Factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:SslMode"] = sslMode,
                ["Proxy:TrustedNetworks"] = proxy,
                ["Legal:PublicBaseUrl"] = publicBaseUrl,
            }));
        });

    private static List<string> Messages(Exception ex)
    {
        var messages = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException) messages.Add(e.Message);
        if (ex is AggregateException agg) messages.AddRange(agg.InnerExceptions.Select(e => e.Message));
        return messages;
    }

    [Fact]
    public async Task Openapi_no_se_publica_en_produccion()
    {
        await using var factory = Production("Require");
        await factory.CreateClient().GetAsync("/openapi/v1.json", Ct).Expect(HttpStatusCode.NotFound);
        // El resto de la API sí responde.
        await factory.CreateClient().GetAsync("/health/live", Ct).Expect(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Prefer")]
    [InlineData("Disable")]
    [InlineData("Allow")]
    public void Produccion_sin_tls_exigido_no_arranca(string sslMode)
    {
        using var factory = Production(sslMode);

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains(Messages(ex), m => m.Contains("DB_SSL_MODE", StringComparison.Ordinal));
    }

    [Fact]
    public void Produccion_sin_proxy_de_confianza_ni_url_publica_no_arranca()
    {
        using var factory = Production("Require", proxy: "", publicBaseUrl: "");

        var messages = Messages(Assert.ThrowsAny<Exception>(() => factory.CreateClient()));
        Assert.Contains(messages, m => m.Contains("FORWARDED_TRUSTED_NETWORKS", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("PUBLIC_BASE_URL", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Require")]
    [InlineData("VerifyCA")]
    [InlineData("VerifyFull")]
    public void Produccion_con_tls_exigido_arranca(string sslMode)
    {
        using var factory = Production(sslMode);
        using var client = factory.CreateClient();
    }

    // ---------- 15. Radar con el mismo corte que el like ----------

    private async Task<Guid> EventStartedAsync(TimeSpan ago, TestUser attendee)
    {
        var ev = new Event { Name = $"Evento {Guid.NewGuid():N}", Venue = "Teatro", City = "Bogotá", StartsAt = DateTimeOffset.UtcNow - ago };
        await api.WithDbAsync(async db =>
        {
            db.Events.Add(ev);
            db.EventAttendances.Add(new EventAttendance { EventId = ev.Id, UserId = attendee.Id });
            var genreId = await db.Genres.Select(g => g.Id).FirstAsync(Ct);
            for (var i = 0; i < 3; i++)
            {
                var song = new Song { Title = $"Canción {Guid.NewGuid():N}", Artist = "Artista", GenreId = genreId, CoverUrl = "https://example.test/c.jpg" };
                db.Songs.Add(song);
                db.Swipes.Add(new Swipe { UserId = attendee.Id, SongId = song.Id, Direction = SwipeDirection.Like });
            }
            await db.SaveChangesAsync(Ct);
        });
        return ev.Id;
    }

    [Fact]
    public async Task Radar_de_un_evento_que_empezo_hace_mas_de_un_dia_da_400_event_past()
    {
        var user = await api.CreateUserAsync();
        var old = await EventStartedAsync(TimeSpan.FromDays(2), user);
        var recent = await EventStartedAsync(TimeSpan.FromHours(12), user);

        await user.Client.GetAsync($"/api/v1/events/{old}/radar", Ct).ExpectProblem(HttpStatusCode.BadRequest, "event_past");
        await user.Client.GetAsync($"/api/v1/events/{recent}/radar", Ct).Expect(HttpStatusCode.OK);
    }

}
