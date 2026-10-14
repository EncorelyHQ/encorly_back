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
        var families = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await api.WithDbAsync(async db =>
        {
            foreach (var family in families)
                db.RefreshTokens.Add(new RefreshToken
                {
                    UserId = a.Id, FamilyId = family, TokenHash = TokenService.CreateRefreshToken().Hash, ExpiresAt = DateTimeOffset.UtcNow.AddDays(10),
                });
            await db.SaveChangesAsync(Ct);
        });

        var sessions = await a.Client.GetAsync("/api/v1/me/sessions", Ct).Expect<List<SessionDto>>(HttpStatusCode.OK);
        Assert.Equal(2, sessions.Count);
        Assert.Empty(await b.Client.GetAsync("/api/v1/me/sessions", Ct).Expect<List<SessionDto>>(HttpStatusCode.OK));

        await b.Client.DeleteAsync($"/api/v1/me/sessions/{families[0]}", Ct).ExpectProblem(HttpStatusCode.NotFound, "session_not_found");
        await a.Client.DeleteAsync($"/api/v1/me/sessions/{families[0]}", Ct).Expect(HttpStatusCode.NoContent);
        Assert.Single(await a.Client.GetAsync("/api/v1/me/sessions", Ct).Expect<List<SessionDto>>(HttpStatusCode.OK));

        await a.Client.DeleteAsync("/api/v1/me/sessions", Ct).Expect(HttpStatusCode.NoContent);
        Assert.Empty(await a.Client.GetAsync("/api/v1/me/sessions", Ct).Expect<List<SessionDto>>(HttpStatusCode.OK));
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
