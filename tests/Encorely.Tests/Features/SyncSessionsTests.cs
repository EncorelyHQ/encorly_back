using System.Net;
using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Sessions;
using Encorely.Api.Features.Sync;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Database;
using Encorely.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Tests.Features;

public sealed class SyncSessionsTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static byte[] Bytes(int n) => System.Security.Cryptography.RandomNumberGenerator.GetBytes(n);

    private static object Backup(int size = 64, string kdf = "argon2id") => new
    {
        version = 1,
        ciphertext = Bytes(size),
        nonce = Bytes(24),
        salt = Bytes(16),
        kdf = new { algorithm = kdf, opsLimit = 3, memLimit = 67108864 },
    };

    [Fact]
    public async Task Respaldo_se_sube_reemplaza_descarga_y_borra_solo_el_mio()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        await a.Client.GetAsync("/api/v1/me/backup", Ct).ExpectProblem(HttpStatusCode.NotFound, "backup_missing");

        await a.Client.PutJson("/api/v1/me/backup", Backup(64)).Expect(HttpStatusCode.OK);
        var info = await a.Client.PutJson("/api/v1/me/backup", Backup(128)).Expect<BackupInfoDto>(HttpStatusCode.OK);
        Assert.Equal(128, info.SizeBytes);

        var full = await a.Client.GetAsync("/api/v1/me/backup", Ct).Expect<BackupDto>(HttpStatusCode.OK);
        Assert.Equal(128, full.Ciphertext.Length);
        Assert.Equal("argon2id", full.Kdf.Algorithm);

        // B no ve ni borra el respaldo de A: cada uno solo opera sobre el suyo.
        await b.Client.GetAsync("/api/v1/me/backup", Ct).ExpectProblem(HttpStatusCode.NotFound);
        await b.Client.DeleteAsync("/api/v1/me/backup", Ct).Expect(HttpStatusCode.NoContent);
        await a.Client.GetAsync("/api/v1/me/backup/info", Ct).Expect(HttpStatusCode.OK);

        await a.Client.DeleteAsync("/api/v1/me/backup", Ct).Expect(HttpStatusCode.NoContent);
        await a.Client.GetAsync("/api/v1/me/backup/info", Ct).ExpectProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Respaldo_invalido_da_400()
    {
        var a = await api.CreateUserAsync();
        await a.Client.PutJson("/api/v1/me/backup", Backup(kdf: "md5")).ExpectProblem(HttpStatusCode.BadRequest, "unsupported_kdf");
        await a.Client.PutJson("/api/v1/me/backup", new { version = 1, ciphertext = Bytes(32), nonce = Bytes(12), salt = Bytes(16), kdf = new { algorithm = "argon2id", opsLimit = 1, memLimit = 1 } })
            .ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Primer_respaldo_concurrente_no_da_500()
    {
        var a = await api.CreateUserAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => a.Client.PutJson("/api/v1/me/backup", Backup(64))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        await a.Client.GetAsync("/api/v1/me/backup/info", Ct).Expect(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cotas_de_tamano_y_de_parametros()
    {
        var a = await api.CreateUserAsync();
        object Kdf(string algorithm, long ops, long mem) => new { version = 1, ciphertext = Bytes(32), nonce = Bytes(24), salt = Bytes(16), kdf = new { algorithm, opsLimit = ops, memLimit = mem } };

        // El respaldo admite su máximo de 10 MiB aunque el tope general sea 256 KiB.
        await a.Client.PutJson("/api/v1/me/backup", Backup(10 * 1024 * 1024)).Expect(HttpStatusCode.OK);
        await a.Client.PutJson("/api/v1/me/backup", Kdf("argon2id", 21, 67108864)).ExpectProblem(HttpStatusCode.BadRequest, "unsupported_kdf");
        await a.Client.PutJson("/api/v1/me/backup", Kdf("pbkdf2-sha256", 10_000_001, 1)).ExpectProblem(HttpStatusCode.BadRequest, "unsupported_kdf");
        await a.Client.PutJson("/api/v1/me/backup", Kdf("scrypt", 1, (1L << 30) + 1)).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await a.Client.PutJson("/api/v1/me/backup", Kdf("scrypt", 1L << 20, 1L << 30)).Expect(HttpStatusCode.OK);

        await a.Client.PostJson("/api/v1/me/spotify/import", new { topArtists = new[] { new { name = "x", genres = new[] { new string('g', 101) } } } })
            .ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");

    }

    [Fact]
    public void Tope_de_cuerpo_de_256_KiB_salvo_respaldo_y_reportes()
    {
        // TestServer no aplica los límites de Kestrel: se verifica la configuración que Kestrel y el routing sí aplican.
        var kestrel = api.Service<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>>().Value;
        Assert.Equal(256 * 1024, kestrel.Limits.MaxRequestBodySize);

        var limits = api.Service<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .Select(e => (Name: e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>()?.EndpointName,
                Limit: e.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata>()?.MaxRequestBodySize))
            .Where(e => e.Limit is not null)
            .ToDictionary(e => e.Name!, e => e.Limit);
        Assert.Equal(new Dictionary<string, long?> { ["PutBackup"] = 15 * 1024 * 1024, ["ReportUser"] = 1024 * 1024 }, limits);
    }

    [Fact]
    public async Task Vinculacion_de_dispositivo_es_de_un_solo_uso_y_solo_del_mismo_usuario()
    {
        var a = await api.CreateUserAsync();
        var intruder = await api.CreateUserAsync();
        var link = await a.Client.PostJson("/api/v1/keys/link-requests", new { ephemeralKey = Bytes(32) }).Expect<LinkRequestDto>(HttpStatusCode.Created);
        Assert.Equal(LinkRequestStatus.Pending, link.Status);

        await intruder.Client.GetAsync($"/api/v1/keys/link-requests/{link.Id}", Ct).ExpectProblem(HttpStatusCode.NotFound);
        var fulfill = new { senderEphemeralKey = Bytes(32), nonce = Bytes(24), ciphertext = Bytes(200) };
        await intruder.Client.PutJson($"/api/v1/keys/link-requests/{link.Id}", fulfill).ExpectProblem(HttpStatusCode.NotFound);

        await a.Client.PutJson($"/api/v1/keys/link-requests/{link.Id}", fulfill).Expect(HttpStatusCode.NoContent);
        await a.Client.PutJson($"/api/v1/keys/link-requests/{link.Id}", fulfill).ExpectProblem(HttpStatusCode.Conflict, "link_already_fulfilled");

        var ready = await a.Client.GetAsync($"/api/v1/keys/link-requests/{link.Id}", Ct).Expect<LinkRequestDto>(HttpStatusCode.OK);
        Assert.Equal(LinkRequestStatus.Ready, ready.Status);
        Assert.Equal(200, ready.Ciphertext!.Length);

        await a.Client.DeleteAsync($"/api/v1/keys/link-requests/{link.Id}", Ct).Expect(HttpStatusCode.NoContent);
        await a.Client.GetAsync($"/api/v1/keys/link-requests/{link.Id}", Ct).ExpectProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Vinculacion_limita_buzones_abiertos_y_la_limpieza_borra_los_vencidos()
    {
        var a = await api.CreateUserAsync();
        for (var i = 0; i < 3; i++)
            await a.Client.PostJson("/api/v1/keys/link-requests", new { ephemeralKey = Bytes(32) }).Expect(HttpStatusCode.Created);
        await a.Client.PostJson("/api/v1/keys/link-requests", new { ephemeralKey = Bytes(32) }).ExpectProblem(HttpStatusCode.TooManyRequests);

        await api.WithDbAsync(db => db.Set<DeviceLinkRequest>().Where(l => l.UserId == a.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct));
        await api.Service<MaintenanceService>().RunOnceAsync(Ct);
        Assert.False(await api.WithDbAsync(db => db.Set<DeviceLinkRequest>().AnyAsync(l => l.UserId == a.Id, Ct)));
    }

    [Fact]
    public async Task Sesiones_se_listan_y_se_revocan_solo_las_mias()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        var (otherSession, _, otherDevice) = await api.OpenSessionAsync(a.Id);

        var sessions = await a.Client.GetAsync("/api/v1/me/sessions", Ct).Expect<List<SessionDto>>(HttpStatusCode.OK);
        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions, s => s.Current);
        Assert.Single(await b.Client.GetAsync("/api/v1/me/sessions", Ct).Expect<List<SessionDto>>(HttpStatusCode.OK));

        await b.Client.DeleteAsync($"/api/v1/me/sessions/{otherSession}", Ct).ExpectProblem(HttpStatusCode.NotFound, "session_not_found");
        await a.Client.DeleteAsync($"/api/v1/me/sessions/{otherSession}", Ct).Expect(HttpStatusCode.NoContent);
        Assert.Single(await a.Client.GetAsync("/api/v1/me/sessions", Ct).Expect<List<SessionDto>>(HttpStatusCode.OK));

        await a.Client.DeleteAsync("/api/v1/me/sessions", Ct).Expect(HttpStatusCode.NoContent);
        Assert.Empty(await api.WithDbAsync(db => db.RefreshTokens.Where(t => t.UserId == a.Id && t.RevokedAt == null).ToListAsync(Ct)));
    }

    [Fact]
    public async Task Cerrar_una_sesion_invalida_su_access_token_y_su_hub_al_instante()
    {
        var user = await api.CreateUserAsync();
        var (lostPhone, lostToken, lostClient) = await api.OpenSessionAsync(user.Id);
        await using var lostHub = ChatKit.Hub(api, lostToken);
        await using var ownHub = ChatKit.Hub(api, user.AccessToken);
        var lostClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lostHub.Closed += _ => { lostClosed.TrySetResult(); return Task.CompletedTask; };
        await lostHub.StartAsync(Ct);
        await ownHub.StartAsync(Ct);
        await lostClient.GetAsync("/api/v1/me", Ct).Expect(HttpStatusCode.OK);

        await user.Client.DeleteAsync($"/api/v1/me/sessions/{lostPhone}", Ct).Expect(HttpStatusCode.NoContent);

        await lostClosed.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        foreach (var path in new[] { "/api/v1/me", "/api/v1/me/export", "/api/v1/me/backup" })
            await lostClient.GetAsync(path, Ct).ExpectProblem(HttpStatusCode.Unauthorized, "unauthorized");
        await lostClient.DeleteMe().ExpectProblem(HttpStatusCode.Unauthorized, "unauthorized");
        // La otra sesión del mismo usuario sigue viva.
        Assert.Equal(Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected, ownHub.State);
        await user.Client.GetAsync("/api/v1/me", Ct).Expect(HttpStatusCode.OK);
    }
}

public sealed class RetentionTests(ApiFixture api)
{
    [Fact]
    public async Task Retencion_borra_solo_mensajes_vencidos_cuando_esta_configurada()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        var (ua, ub) = Encorely.Api.Features.Matching.Match.OrderPair(a.Id, b.Id);
        var match = new Encorely.Api.Features.Matching.Match { UserAId = ua, UserBId = ub, LastSeq = 2 };
        Encorely.Api.Features.Chat.ChatMessage Msg(long seq, int ageDays) => new()
        {
            Id = Guid.NewGuid(), MatchId = match.Id, SenderId = a.Id, Seq = seq, Version = 1, SenderBoxKeyId = 1, RecipientBoxKeyId = 1,
            Nonce = new byte[24], Ciphertext = new byte[32], CreatedAt = DateTimeOffset.UtcNow.AddDays(-ageDays),
        };
        await api.WithDbAsync(async db =>
        {
            db.Matches.Add(match);
            db.ChatMessages.AddRange(Msg(1, 40), Msg(2, 1));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var maintenance = new MaintenanceService(api.Service<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new Encorely.Api.Infrastructure.Configuration.ChatOptions { MessageRetentionDays = 30 }),
            TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<MaintenanceService>.Instance);
        await maintenance.RunOnceAsync(TestContext.Current.CancellationToken);

        var left = await api.WithDbAsync(db => db.ChatMessages.Where(m => m.MatchId == match.Id).Select(m => m.Seq).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal([2L], left);
    }
}
