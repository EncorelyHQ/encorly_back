using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Encorely.Api.Features.Chat;
using Encorely.Api.Features.Events;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Moderation;
using Encorely.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Tests.Features;

public sealed class MatchingModerationTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> SharedEventAsync(params TestUser[] users)
    {
        var ev = new Event { Name = "Festival de pruebas", Venue = "Movistar Arena", City = "Bogotá", StartsAt = DateTimeOffset.UtcNow.AddDays(10) };
        await api.WithDbAsync(async db =>
        {
            db.Events.Add(ev);
            foreach (var u in users) db.EventAttendances.Add(new EventAttendance { EventId = ev.Id, UserId = u.Id });
            await db.SaveChangesAsync(Ct);
        });
        return ev.Id;
    }

    private static Task<HttpResponseMessage> Like(TestUser from, TestUser to, Guid? eventId = null)
        => from.Client.PostJson("/api/v1/likes", new { targetUserId = to.Id, eventId });

    private static async Task<LikeResponse> LikeOk(TestUser from, TestUser to, Guid? eventId = null)
        => await Like(from, to, eventId).Expect<LikeResponse>(HttpStatusCode.OK);

    private async Task<(TestUser A, TestUser B, Guid Match)> MatchedPairAsync()
    {
        var a = await api.CreateUserAsync("Ana Prueba");
        var b = await api.CreateUserAsync("Beto Prueba");
        var eventId = await SharedEventAsync(a, b);
        await LikeOk(a, b, eventId);
        var r = await LikeOk(b, a);
        Assert.True(r.Matched);
        return (a, b, r.MatchId!.Value);
    }

    private Task<int> CountMatchesAsync(Guid x, Guid y, MatchStatus? status = null)
    {
        var (a, b) = Match.OrderPair(x, y);
        return api.WithDbAsync(db => db.Matches.CountAsync(m => m.UserAId == a && m.UserBId == b && (status == null || m.Status == status), Ct));
    }

    private static Task<List<MatchDto>> ListMatches(TestUser u)
        => u.Client.GetAsync("/api/v1/matches", Ct).Expect<List<MatchDto>>(HttpStatusCode.OK);

    [Fact]
    public async Task Reactivar_una_cuenta_suspendida_no_rehace_el_match_con_un_like()
    {
        var (a, b, _) = await MatchedPairAsync();
        var admin = await api.CreateUserAsync("Admin", u => u.Role = Encorely.Api.Features.Users.UserRole.Admin);
        await admin.Client.PutJson($"/api/v1/admin/users/{b.Id}/status", new { status = "banned" }).Expect(HttpStatusCode.NoContent);
        await admin.Client.PutJson($"/api/v1/admin/users/{b.Id}/status", new { status = "active" }).Expect(HttpStatusCode.NoContent);
        Assert.False(await api.WithDbAsync(db => db.UserLikes.AnyAsync(l => l.FromUserId == b.Id || l.ToUserId == b.Id, Ct)));

        // El ban cerró las sesiones de B: entra de nuevo.
        var (_, token, client) = await api.OpenSessionAsync(b.Id);
        var reactivated = b with { AccessToken = token, Client = client };
        Assert.False((await LikeOk(reactivated, a)).Matched);
        Assert.Equal(0, await CountMatchesAsync(a.Id, b.Id, MatchStatus.Active));
        Assert.True((await LikeOk(a, reactivated)).Matched);
    }

    [Fact]
    public async Task Like_unilateral_no_crea_match()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        await SharedEventAsync(a, b);

        var r = await LikeOk(a, b);

        Assert.False(r.Matched);
        Assert.Null(r.MatchId);
        Assert.Equal(0, await CountMatchesAsync(a.Id, b.Id));
    }

    [Fact]
    public async Task Like_reciproco_crea_un_match_y_ambos_ven_el_nombre_real_del_otro()
    {
        var (a, b, matchId) = await MatchedPairAsync();

        Assert.Equal(1, await CountMatchesAsync(a.Id, b.Id));
        var seenByA = Assert.Single(await ListMatches(a), m => m.Id == matchId);
        var seenByB = Assert.Single(await ListMatches(b), m => m.Id == matchId);
        Assert.Equal("Beto Prueba", seenByA.Peer.DisplayName);
        Assert.Equal("Ana Prueba", seenByB.Peer.DisplayName);
        Assert.Equal(b.Id, seenByA.Peer.Id);
        Assert.NotNull(seenByA.Peer.Age);
        Assert.Contains("reggaeton", seenByA.SharedGenres);
        Assert.Equal("Festival de pruebas", seenByA.EventName);
    }

    [Fact]
    public async Task Likes_reciprocos_concurrentes_crean_exactamente_un_match_activo()
    {
        for (var round = 0; round < 20; round++)
        {
            var a = await api.CreateUserAsync();
            var b = await api.CreateUserAsync();
            await SharedEventAsync(a, b);

            var responses = await Task.WhenAll(LikeOk(a, b), LikeOk(b, a));

            Assert.Equal(1, await CountMatchesAsync(a.Id, b.Id, MatchStatus.Active));
            Assert.Contains(responses, r => r.Matched);
            Assert.All(responses.Where(r => r.Matched), r => Assert.NotNull(r.MatchId));
        }
    }

    [Fact]
    public async Task Like_es_idempotente()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        await SharedEventAsync(a, b);

        await LikeOk(a, b);
        var again = await LikeOk(a, b);

        Assert.False(again.Matched);
        Assert.Equal(1, await api.WithDbAsync(db => db.UserLikes.CountAsync(l => l.FromUserId == a.Id && l.ToUserId == b.Id, Ct)));

        var (c, d, matchId) = await MatchedPairAsync();
        var repeat = await LikeOk(c, d);
        Assert.True(repeat.Matched);
        Assert.Equal(matchId, repeat.MatchId);
        Assert.Equal(1, await CountMatchesAsync(c.Id, d.Id));
    }

    [Fact]
    public async Task Like_a_si_mismo_da_400()
    {
        var a = await api.CreateUserAsync();
        await Like(a, a).ExpectProblem(HttpStatusCode.BadRequest, "cannot_like_self");
    }

    [Fact]
    public async Task Like_a_usuario_inexistente_da_404_y_guid_vacio_400()
    {
        var a = await api.CreateUserAsync();
        await a.Client.PostJson("/api/v1/likes", new { targetUserId = Guid.NewGuid() }).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
        await a.Client.PostJson("/api/v1/likes", new { targetUserId = Guid.Empty }).ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Like_sin_evento_compartido_da_403()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        await SharedEventAsync(a);
        await SharedEventAsync(b);

        await Like(a, b).ExpectProblem(HttpStatusCode.Forbidden, "no_shared_event");
    }

    [Fact]
    public async Task Like_con_evento_pasado_o_no_compartido_da_403()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        var shared = await SharedEventAsync(a, b);
        var onlyMine = await SharedEventAsync(a);

        await Like(a, b, onlyMine).ExpectProblem(HttpStatusCode.Forbidden, "no_shared_event");
        await Like(a, b, Guid.NewGuid()).ExpectProblem(HttpStatusCode.Forbidden, "no_shared_event");
        Assert.False((await LikeOk(a, b, shared)).Matched);

        var c = await api.CreateUserAsync();
        var d = await api.CreateUserAsync();
        var past = new Event { Name = "Viejo", Venue = "X", City = "Bogotá", StartsAt = DateTimeOffset.UtcNow.AddDays(-3) };
        await api.WithDbAsync(async db =>
        {
            db.Events.Add(past);
            db.EventAttendances.Add(new EventAttendance { EventId = past.Id, UserId = c.Id });
            db.EventAttendances.Add(new EventAttendance { EventId = past.Id, UserId = d.Id });
            await db.SaveChangesAsync(Ct);
        });
        await Like(c, d).ExpectProblem(HttpStatusCode.Forbidden, "no_shared_event");
    }

    [Fact]
    public async Task Like_a_bloqueado_o_a_quien_me_bloqueo_da_404()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        await SharedEventAsync(a, b);

        await a.Client.PostJson("/api/v1/blocks", new { userId = b.Id }).Expect(HttpStatusCode.NoContent);

        await Like(a, b).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
        await Like(b, a).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
    }

    [Fact]
    public async Task Un_tercero_no_puede_ver_ni_deshacer_el_match_ajeno()
    {
        var (_, _, matchId) = await MatchedPairAsync();
        var c = await api.CreateUserAsync();

        await c.Client.GetAsync($"/api/v1/matches/{matchId}", Ct).ExpectProblem(HttpStatusCode.NotFound, "match_not_found");
        await c.Client.DeleteAsync($"/api/v1/matches/{matchId}", Ct).ExpectProblem(HttpStatusCode.NotFound, "match_not_found");
        Assert.DoesNotContain(await ListMatches(c), m => m.Id == matchId);
        Assert.Equal(1, await CountMatchesAsync((await MatchPeers(matchId)).A, (await MatchPeers(matchId)).B, MatchStatus.Active));
    }

    private Task<(Guid A, Guid B)> MatchPeers(Guid matchId)
        => api.WithDbAsync(async db =>
        {
            var m = await db.Matches.AsNoTracking().SingleAsync(x => x.Id == matchId, Ct);
            return (m.UserAId, m.UserBId);
        });

    [Fact]
    public async Task Unmatch_cierra_sale_de_la_lista_y_permite_un_match_nuevo_tras_nuevos_likes()
    {
        var (a, b, first) = await MatchedPairAsync();

        await a.Client.DeleteAsync($"/api/v1/matches/{first}", Ct).Expect(HttpStatusCode.NoContent);

        Assert.DoesNotContain(await ListMatches(a), m => m.Id == first);
        Assert.DoesNotContain(await ListMatches(b), m => m.Id == first);
        var closed = await api.WithDbAsync(db => db.Matches.AsNoTracking().SingleAsync(m => m.Id == first, Ct));
        Assert.Equal(MatchStatus.Closed, closed.Status);
        Assert.Equal(MatchCloseReason.Unmatched, closed.CloseReason);
        Assert.Equal(a.Id, closed.ClosedByUserId);
        // El historial sigue consultable por id, pero deshacer dos veces es 404.
        var got = await a.Client.GetAsync($"/api/v1/matches/{first}", Ct).Expect<MatchDto>(HttpStatusCode.OK);
        Assert.Equal(MatchStatus.Closed, got.Status);
        await a.Client.DeleteAsync($"/api/v1/matches/{first}", Ct).ExpectProblem(HttpStatusCode.NotFound, "match_not_found");

        // Un solo like no basta: los likes anteriores se borraron.
        Assert.False((await LikeOk(a, b)).Matched);
        var again = await LikeOk(b, a);
        Assert.True(again.Matched);
        Assert.NotEqual(first, again.MatchId);
        Assert.Equal(1, await CountMatchesAsync(a.Id, b.Id, MatchStatus.Active));
    }

    [Fact]
    public async Task Bloquear_cierra_el_match_borra_likes_y_el_bloqueado_no_puede_dar_like()
    {
        var (a, b, matchId) = await MatchedPairAsync();

        await a.Client.PostJson("/api/v1/blocks", new { userId = b.Id }).Expect(HttpStatusCode.NoContent);
        await a.Client.PostJson("/api/v1/blocks", new { userId = b.Id }).Expect(HttpStatusCode.NoContent); // idempotente

        var m = await api.WithDbAsync(db => db.Matches.AsNoTracking().SingleAsync(x => x.Id == matchId, Ct));
        Assert.Equal(MatchStatus.Closed, m.Status);
        Assert.Equal(MatchCloseReason.Blocked, m.CloseReason);
        Assert.Equal(0, await api.WithDbAsync(db => db.UserLikes.CountAsync(l =>
            (l.FromUserId == a.Id && l.ToUserId == b.Id) || (l.FromUserId == b.Id && l.ToUserId == a.Id), Ct)));
        Assert.DoesNotContain(await ListMatches(b), x => x.Id == matchId);
        await Like(b, a).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
        Assert.Equal(1, await api.WithDbAsync(db => db.Blocks.CountAsync(x => x.BlockerId == a.Id && x.BlockedId == b.Id, Ct)));
    }

    [Fact]
    public async Task Unblock_no_reabre_el_match_y_es_idempotente()
    {
        var (a, b, matchId) = await MatchedPairAsync();
        await a.Client.PostJson("/api/v1/blocks", new { userId = b.Id }).Expect(HttpStatusCode.NoContent);

        await a.Client.DeleteAsync($"/api/v1/blocks/{b.Id}", Ct).Expect(HttpStatusCode.NoContent);
        await a.Client.DeleteAsync($"/api/v1/blocks/{b.Id}", Ct).Expect(HttpStatusCode.NoContent);
        await a.Client.DeleteAsync($"/api/v1/blocks/{Guid.NewGuid()}", Ct).Expect(HttpStatusCode.NoContent);

        Assert.DoesNotContain(await ListMatches(a), x => x.Id == matchId);
        Assert.Equal(0, await CountMatchesAsync(a.Id, b.Id, MatchStatus.Active));
    }

    [Fact]
    public async Task Bloquear_a_si_mismo_400_e_inexistente_404()
    {
        var a = await api.CreateUserAsync();
        await a.Client.PostJson("/api/v1/blocks", new { userId = a.Id }).ExpectProblem(HttpStatusCode.BadRequest, "cannot_block_self");
        await a.Client.PostJson("/api/v1/blocks", new { userId = Guid.NewGuid() }).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
    }

    [Fact]
    public async Task Lista_de_bloqueos_solo_muestra_los_mios()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync("Bloqueado Uno");
        var c = await api.CreateUserAsync();
        var d = await api.CreateUserAsync("Bloqueado Dos");

        await a.Client.PostJson("/api/v1/blocks", new { userId = b.Id }).Expect(HttpStatusCode.NoContent);
        await c.Client.PostJson("/api/v1/blocks", new { userId = d.Id }).Expect(HttpStatusCode.NoContent);

        var mine = await a.Client.GetAsync("/api/v1/blocks", Ct).Expect<List<BlockedUserDto>>(HttpStatusCode.OK);
        var blocked = Assert.Single(mine);
        Assert.Equal(b.Id, blocked.UserId);
        Assert.Equal("Bloqueado Uno", blocked.DisplayName);
        // Quien es bloqueado no ve a quien lo bloqueó en su lista.
        Assert.Empty(await b.Client.GetAsync("/api/v1/blocks", Ct).Expect<List<BlockedUserDto>>(HttpStatusCode.OK));
        // Desbloquear con el id de otro no toca los bloqueos ajenos.
        await a.Client.DeleteAsync($"/api/v1/blocks/{d.Id}", Ct).Expect(HttpStatusCode.NoContent);
        Assert.Single(await c.Client.GetAsync("/api/v1/blocks", Ct).Expect<List<BlockedUserDto>>(HttpStatusCode.OK));
    }

    // --- Reportes y franking ---

    private static string Envelope(Guid messageId, Guid matchId, Guid sender, Guid recipient, string body)
        => JsonSerializer.Serialize(new { v = 1, id = messageId, m = matchId, s = sender, r = recipient, t = 1760000000000L, k = "text", b = body });

    private static byte[] Tag(byte[] key, Guid messageId, string plaintext)
    {
        var data = Encoding.UTF8.GetBytes("ENCORELY-FRANK-v1").Concat(messageId.ToByteArray(bigEndian: true)).Concat(Encoding.UTF8.GetBytes(plaintext)).ToArray();
        return HMACSHA256.HashData(key, data);
    }

    private async Task<Guid> InsertMessageAsync(Guid matchId, Guid sender, long seq, byte[]? tag)
    {
        var id = Guid.NewGuid();
        await api.WithDbAsync(async db =>
        {
            db.ChatMessages.Add(new ChatMessage
            {
                Id = id, MatchId = matchId, SenderId = sender, Seq = seq, Version = 1,
                Nonce = RandomNumberGenerator.GetBytes(24), Ciphertext = RandomNumberGenerator.GetBytes(40), FrankingTag = tag,
            });
            await db.SaveChangesAsync(Ct);
        });
        return id;
    }

    private static object Evidence(Guid id, string plaintext, byte[] key) => new { messageId = id, plaintext, frankingKey = key };

    private async Task<List<ReportEvidence>> StoredEvidenceAsync(Guid reportId)
        => (await api.WithDbAsync(db => db.Reports.AsNoTracking().SingleAsync(r => r.Id == reportId, Ct))).Evidence;

    [Fact]
    public async Task Reporte_con_evidencia_franked_correcta_queda_verificado_y_no_bloquea()
    {
        var (a, b, matchId) = await MatchedPairAsync();
        var key = RandomNumberGenerator.GetBytes(32);
        var id = Guid.NewGuid();
        var plaintext = Envelope(id, matchId, b.Id, a.Id, "mensaje ofensivo");
        await api.WithDbAsync(async db =>
        {
            db.ChatMessages.Add(new ChatMessage
            {
                Id = id, MatchId = matchId, SenderId = b.Id, Seq = 1, Version = 1,
                Nonce = RandomNumberGenerator.GetBytes(24), Ciphertext = RandomNumberGenerator.GetBytes(40), FrankingTag = Tag(key, id, plaintext),
            });
            await db.SaveChangesAsync(Ct);
        });

        var created = await a.Client.PostJson("/api/v1/reports", new
        {
            userId = b.Id, reason = "harassment", detail = "  me insulta  ", matchId, evidence = new[] { Evidence(id, plaintext, key) },
        }).Expect<ReportCreatedDto>(HttpStatusCode.Accepted);

        var report = await api.WithDbAsync(db => db.Reports.AsNoTracking().SingleAsync(r => r.Id == created.Id, Ct));
        Assert.Equal("me insulta", report.Detail);
        Assert.Equal(a.Id, report.ReporterId);
        Assert.Equal(b.Id, report.ReportedUserId);
        var ev = Assert.Single(report.Evidence);
        Assert.True(ev.Verified);
        Assert.Equal(plaintext, ev.Plaintext);
        // Reportar no bloquea ni cierra el match.
        Assert.Equal(1, await CountMatchesAsync(a.Id, b.Id, MatchStatus.Active));
        Assert.Empty(await a.Client.GetAsync("/api/v1/blocks", Ct).Expect<List<BlockedUserDto>>(HttpStatusCode.OK));
    }

    [Fact]
    public async Task Evidencia_alterada_sin_tag_o_con_sobre_incoherente_no_se_verifica()
    {
        var (a, b, matchId) = await MatchedPairAsync();
        var key = RandomNumberGenerator.GetBytes(32);

        async Task<(Guid Id, string Plain)> Franked(long seq, Guid sender, Guid recipient, Guid? envelopeMatch = null)
        {
            var id = Guid.NewGuid();
            var plain = Envelope(id, envelopeMatch ?? matchId, sender, recipient, "hola");
            await api.WithDbAsync(async db =>
            {
                db.ChatMessages.Add(new ChatMessage
                {
                    Id = id, MatchId = matchId, SenderId = b.Id, Seq = seq, Version = 1,
                    Nonce = RandomNumberGenerator.GetBytes(24), Ciphertext = RandomNumberGenerator.GetBytes(40), FrankingTag = Tag(key, id, plain),
                });
                await db.SaveChangesAsync(Ct);
            });
            return (id, plain);
        }

        var tampered = await Franked(1, b.Id, a.Id);
        var wrongRecipient = await Franked(2, b.Id, Guid.NewGuid());
        var wrongMatch = await Franked(3, b.Id, a.Id, Guid.NewGuid());
        var notJson = Guid.NewGuid();
        await api.WithDbAsync(async db =>
        {
            db.ChatMessages.Add(new ChatMessage
            {
                Id = notJson, MatchId = matchId, SenderId = b.Id, Seq = 4, Version = 1,
                Nonce = RandomNumberGenerator.GetBytes(24), Ciphertext = RandomNumberGenerator.GetBytes(40), FrankingTag = Tag(key, notJson, "texto plano"),
            });
            await db.SaveChangesAsync(Ct);
        });
        var untagged = await InsertMessageAsync(matchId, b.Id, 5, null);
        var untaggedPlain = Envelope(untagged, matchId, b.Id, a.Id, "sin tag");

        var created = await a.Client.PostJson("/api/v1/reports", new
        {
            userId = b.Id, reason = "spam", evidence = new[]
            {
                Evidence(tampered.Id, tampered.Plain.Replace("hola", "adios"), key),
                Evidence(wrongRecipient.Id, wrongRecipient.Plain, key),
                Evidence(wrongMatch.Id, wrongMatch.Plain, key),
                Evidence(notJson, "texto plano", key),
                Evidence(untagged, untaggedPlain, key),
            },
        }).Expect<ReportCreatedDto>(HttpStatusCode.Accepted);

        var evidence = await StoredEvidenceAsync(created.Id);
        Assert.Equal(5, evidence.Count);
        Assert.All(evidence, e => Assert.False(e.Verified));
    }

    [Fact]
    public async Task Evidencia_con_llave_incorrecta_no_se_verifica()
    {
        var (a, b, matchId) = await MatchedPairAsync();
        var id = Guid.NewGuid();
        var plain = Envelope(id, matchId, b.Id, a.Id, "hola");
        await api.WithDbAsync(async db =>
        {
            db.ChatMessages.Add(new ChatMessage
            {
                Id = id, MatchId = matchId, SenderId = b.Id, Seq = 1, Version = 1,
                Nonce = RandomNumberGenerator.GetBytes(24), Ciphertext = RandomNumberGenerator.GetBytes(40),
                FrankingTag = Tag(RandomNumberGenerator.GetBytes(32), id, plain),
            });
            await db.SaveChangesAsync(Ct);
        });

        var created = await a.Client.PostJson("/api/v1/reports", new
        {
            userId = b.Id, reason = "other", evidence = new[] { Evidence(id, plain, RandomNumberGenerator.GetBytes(32)) },
        }).Expect<ReportCreatedDto>(HttpStatusCode.Accepted);

        Assert.False(Assert.Single(await StoredEvidenceAsync(created.Id)).Verified);
    }

    [Fact]
    public async Task Matches_se_paginan_por_keyset_y_sin_limit_responden_como_antes()
    {
        var me = await api.CreateUserAsync("Paginadora");
        var at = DateTimeOffset.UtcNow.AddMinutes(-5);
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var other = await api.CreateUserAsync();
            ids.Add(await ChatKit.CreateMatchAsync(api, me, other));
        }
        // Tres empatan en la misma fecha: el id desempata.
        await api.WithDbAsync(async db =>
        {
            await db.Matches.Where(m => ids.Contains(m.Id)).ExecuteUpdateAsync(u => u.SetProperty(m => m.CreatedAt, at), Ct);
            await db.Matches.Where(m => m.Id == ids[0]).ExecuteUpdateAsync(u => u.SetProperty(m => m.LastMessageAt, at.AddMinutes(2)), Ct);
            await db.Matches.Where(m => m.Id == ids[1]).ExecuteUpdateAsync(u => u.SetProperty(m => m.LastMessageAt, at.AddMinutes(1)), Ct);
        });

        var everything = await me.Client.GetAsync("/api/v1/matches", Ct).Expect<List<MatchDto>>(HttpStatusCode.OK);
        Assert.Equal(5, everything.Count);

        var paged = new List<MatchDto>();
        var query = "?limit=2";
        while (true)
        {
            var page = await me.Client.GetAsync("/api/v1/matches" + query, Ct).Expect<List<MatchDto>>(HttpStatusCode.OK);
            paged.AddRange(page);
            if (page.Count < 2) break;
            var last = page[^1];
            query = $"?limit=2&beforeAt={Uri.EscapeDataString((last.LastMessageAt ?? last.CreatedAt).ToString("O"))}&beforeId={last.Id}";
        }
        Assert.Equal(everything.Select(m => m.Id), paged.Select(m => m.Id));
        Assert.Equal([ids[0], ids[1]], paged.Take(2).Select(m => m.Id));

        await me.Client.GetAsync($"/api/v1/matches?beforeId={ids[0]}", Ct).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await me.Client.GetAsync("/api/v1/matches?limit=0", Ct).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Reportar_otra_vez_a_la_misma_persona_suma_al_reporte_abierto()
    {
        var (a, b, matchId) = await MatchedPairAsync();
        var key = RandomNumberGenerator.GetBytes(32);
        var first = await InsertMessageAsync(matchId, b.Id, 1, null);
        var second = await InsertMessageAsync(matchId, b.Id, 2, null);

        var created = await a.Client.PostJson("/api/v1/reports", new
        {
            userId = b.Id, reason = "spam", detail = "primero", evidence = new[] { Evidence(first, "x", key) },
        }).Expect<ReportCreatedDto>(HttpStatusCode.Accepted);
        var createdAt = (await api.WithDbAsync(db => db.Reports.AsNoTracking().SingleAsync(r => r.Id == created.Id, Ct))).CreatedAt;

        var again = await a.Client.PostJson("/api/v1/reports", new
        {
            userId = b.Id, reason = "harassment", matchId, evidence = new[] { Evidence(first, "x", key), Evidence(second, "y", key) },
        }).Expect<ReportCreatedDto>(HttpStatusCode.Accepted);
        Assert.Equal(created.Id, again.Id);

        var report = await api.WithDbAsync(db => db.Reports.AsNoTracking().SingleAsync(r => r.ReporterId == a.Id && r.ReportedUserId == b.Id, Ct));
        Assert.Equal(ReportReason.Harassment, report.Reason);
        Assert.Equal("primero", report.Detail);       // sin detalle nuevo se conserva el anterior
        Assert.Equal(matchId, report.MatchId);
        Assert.Equal(createdAt, report.CreatedAt);     // no se adelanta en la cola
        Assert.Equal([first, second], report.Evidence.Select(e => e.MessageId));

        // Ya resuelto, un reporte nuevo abre otro.
        await api.WithDbAsync(async db =>
        {
            await db.Reports.Where(r => r.Id == created.Id).ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, ReportStatus.Dismissed), Ct);
        });
        var reopened = await a.Client.PostJson("/api/v1/reports", new { userId = b.Id, reason = "spam" })
            .Expect<ReportCreatedDto>(HttpStatusCode.Accepted);
        Assert.NotEqual(created.Id, reopened.Id);
    }

    [Fact]
    public async Task Reportes_concurrentes_de_la_misma_persona_quedan_en_uno()
    {
        var (a, b, _) = await MatchedPairAsync();
        var ids = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            a.Client.PostJson("/api/v1/reports", new { userId = b.Id, reason = "spam" }).Expect<ReportCreatedDto>(HttpStatusCode.Accepted)));
        Assert.Single(ids.Select(r => r.Id).Distinct());
    }

    [Fact]
    public async Task Evidencia_de_un_mensaje_que_esa_persona_no_me_envio_da_400()
    {
        var (a, b, matchId) = await MatchedPairAsync();
        var key = RandomNumberGenerator.GetBytes(32);
        var mine = await InsertMessageAsync(matchId, a.Id, 1, null);       // lo envié yo
        var theirs = await InsertMessageAsync(matchId, b.Id, 2, null);

        await a.Client.PostJson("/api/v1/reports", new { userId = b.Id, reason = "spam", evidence = new[] { Evidence(mine, "x", key) } })
            .ExpectProblem(HttpStatusCode.BadRequest, "invalid_evidence");
        await a.Client.PostJson("/api/v1/reports", new { userId = b.Id, reason = "spam", evidence = new[] { Evidence(Guid.NewGuid(), "x", key) } })
            .ExpectProblem(HttpStatusCode.BadRequest, "invalid_evidence");

        // Mensaje de otro match del reportado con un tercero: tampoco vale.
        var c = await api.CreateUserAsync();
        await SharedEventAsync(b, c);
        await LikeOk(b, c);
        var bc = (await LikeOk(c, b)).MatchId!.Value;
        var other = await InsertMessageAsync(bc, b.Id, 1, null);
        await a.Client.PostJson("/api/v1/reports", new { userId = b.Id, reason = "spam", evidence = new[] { Evidence(other, "x", key) } })
            .ExpectProblem(HttpStatusCode.BadRequest, "invalid_evidence");

        // Y c no puede usar el mensaje de a-b contra b.
        await c.Client.PostJson("/api/v1/reports", new { userId = b.Id, reason = "spam", evidence = new[] { Evidence(theirs, "x", key) } })
            .ExpectProblem(HttpStatusCode.BadRequest, "invalid_evidence");
        Assert.Equal(0, await api.WithDbAsync(db => db.Reports.CountAsync(r => r.ReporterId == a.Id, Ct)));
    }

    [Fact]
    public async Task Reporte_con_match_ajeno_da_404_y_validaciones_basicas()
    {
        var (a, b, matchId) = await MatchedPairAsync();
        var c = await api.CreateUserAsync();

        await c.Client.PostJson("/api/v1/reports", new { userId = b.Id, reason = "spam", matchId }).ExpectProblem(HttpStatusCode.NotFound, "match_not_found");
        await a.Client.PostJson("/api/v1/reports", new { userId = c.Id, reason = "spam", matchId }).ExpectProblem(HttpStatusCode.NotFound, "match_not_found");
        await a.Client.PostJson("/api/v1/reports", new { userId = Guid.NewGuid(), reason = "spam" }).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
        await a.Client.PostJson("/api/v1/reports", new { userId = a.Id, reason = "spam" }).ExpectProblem(HttpStatusCode.BadRequest);
        await a.Client.PostJson("/api/v1/reports", new { userId = b.Id, reason = "no-existe" }).ExpectProblem(HttpStatusCode.BadRequest);
        await a.Client.PostJson("/api/v1/reports", new { userId = b.Id, reason = "spam", evidence = new[] { new { messageId = Guid.NewGuid(), plaintext = "x", frankingKey = new byte[3] } } })
            .ExpectProblem(HttpStatusCode.BadRequest);
        // Reportar sin match ni evidencia es válido.
        await a.Client.PostJson("/api/v1/reports", new { userId = c.Id, reason = "fakeProfile" }).Expect(HttpStatusCode.Accepted);
    }

    // --- Tiempo real ---

    private async Task<HubConnection> ConnectAsync(TestUser u, Action<HubConnection> register)
    {
        var token = u.AccessToken;
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Factory.Server.BaseAddress, "/hubs/chat"), o =>
            {
                o.HttpMessageHandlerFactory = _ => api.Factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        register(hub);
        await hub.StartAsync(Ct);
        return hub;
    }

    private static async Task<Guid> Await(TaskCompletionSource<Guid> tcs)
        => await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

    [Fact]
    public async Task SignalR_avisa_MatchCreated_a_ambos_y_MatchClosed_al_deshacer_y_al_bloquear()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        await SharedEventAsync(a, b);

        TaskCompletionSource<Guid> NewTcs() => new(TaskCreationOptions.RunContinuationsAsynchronously);
        var createdA = NewTcs(); var createdB = NewTcs(); var closedA = NewTcs(); var closedB = NewTcs();
        var closedByUnmatchB = NewTcs();

        await using var hubA = await ConnectAsync(a, h =>
        {
            h.On<JsonElement>("MatchCreated", e => createdA.TrySetResult(e.GetProperty("matchId").GetGuid()));
            h.On<JsonElement>("MatchClosed", e => closedA.TrySetResult(e.GetProperty("matchId").GetGuid()));
        });
        await using var hubB = await ConnectAsync(b, h =>
        {
            h.On<JsonElement>("MatchCreated", e => createdB.TrySetResult(e.GetProperty("matchId").GetGuid()));
            h.On<JsonElement>("MatchClosed", e => (closedByUnmatchB.Task.IsCompleted ? closedB : closedByUnmatchB).TrySetResult(e.GetProperty("matchId").GetGuid()));
        });

        await LikeOk(a, b);
        var matchId = (await LikeOk(b, a)).MatchId!.Value;
        Assert.Equal(matchId, await Await(createdA));
        Assert.Equal(matchId, await Await(createdB));

        await a.Client.DeleteAsync($"/api/v1/matches/{matchId}", Ct).Expect(HttpStatusCode.NoContent);
        Assert.Equal(matchId, await Await(closedA));
        Assert.Equal(matchId, await Await(closedByUnmatchB));

        // Nuevo match y ahora cierre por bloqueo.
        await LikeOk(a, b);
        var second = (await LikeOk(b, a)).MatchId!.Value;
        await b.Client.PostJson("/api/v1/blocks", new { userId = a.Id }).Expect(HttpStatusCode.NoContent);
        Assert.Equal(second, await Await(closedB));
    }
}
