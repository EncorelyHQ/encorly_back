using Microsoft.AspNetCore.SignalR.Client;
using System.Net;
using Encorely.Api.Features.Chat;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Infrastructure.Realtime;
using Encorely.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static Encorely.Tests.Features.ChatKit;

namespace Encorely.Tests.Features;

public sealed class ChatTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Messages(Guid matchId) => $"/api/v1/matches/{matchId}/messages";

    [Fact]
    public async Task Enviar_da_201_con_seq_consecutivo()
    {
        var (a, b, matchId) = await PairAsync(api);
        for (var expected = 1; expected <= 3; expected++)
        {
            var sender = expected == 2 ? b : a;
            var r = await SendAsync(sender, matchId, MessageBody(Guid.NewGuid(), 1, 1)).Expect(HttpStatusCode.Created);
            Assert.Equal(expected, (await r.Read<MessageAcceptedDto>()).Seq);
        }
        await api.WithDbAsync(async db =>
        {
            var m = await db.Matches.AsNoTracking().SingleAsync(x => x.Id == matchId, Ct);
            Assert.Equal(3, m.LastSeq);
            Assert.NotNull(m.LastMessageAt);
        });
    }

    [Fact]
    public async Task Reenvio_identico_da_200_mismo_seq_y_otro_contenido_da_409()
    {
        var (a, b, matchId) = await PairAsync(api);
        var id = Guid.NewGuid();
        var nonce = Rand(24);
        var ct = Rand(40);
        var first = await SendAsync(a, matchId, MessageBody(id, 1, 1, ct, nonce)).Expect<MessageAcceptedDto>(HttpStatusCode.Created);
        var again = await SendAsync(a, matchId, MessageBody(id, 1, 1, ct, nonce)).Expect<MessageAcceptedDto>(HttpStatusCode.OK);
        Assert.Equal(first.Seq, again.Seq);

        await SendAsync(a, matchId, MessageBody(id, 1, 1, Rand(40), nonce)).ExpectProblem(HttpStatusCode.Conflict, "message_id_conflict");
        // El mismo id reusado por el otro miembro tampoco es un reintento.
        await SendAsync(b, matchId, MessageBody(id, 1, 1, ct, nonce)).ExpectProblem(HttpStatusCode.Conflict, "message_id_conflict");
        await api.WithDbAsync(async db => Assert.Equal(1, await db.ChatMessages.CountAsync(m => m.MatchId == matchId, Ct)));
    }

    [Fact]
    public async Task Veinte_envios_concurrentes_dan_seq_1_a_20_sin_huecos()
    {
        var (a, b, matchId) = await PairAsync(api);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async i =>
        {
            var from = i % 2 == 0 ? a : b;
            return await SendAsync(from, matchId, MessageBody(Guid.NewGuid(), 1, 1)).Expect<MessageAcceptedDto>(HttpStatusCode.Created);
        }));
        Assert.Equal(Enumerable.Range(1, 20).Select(i => (long)i), results.Select(r => r.Seq).Order());
    }

    [Fact]
    public async Task Envios_concurrentes_con_el_mismo_id_crean_uno_solo()
    {
        var (a, _, matchId) = await PairAsync(api);
        var body = MessageBody(Guid.NewGuid(), 1, 1);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SendAsync(a, matchId, body)));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, $"status {(int)r.StatusCode}"));
        await api.WithDbAsync(async db => Assert.Equal(1, (await db.Matches.AsNoTracking().SingleAsync(m => m.Id == matchId, Ct)).LastSeq));
    }

    [Fact]
    public async Task RecipientBoxKeyId_viejo_da_409_stale_recipient_key()
    {
        var (a, b, matchId) = await PairAsync(api);
        await PutKeysAsync(b, 2);
        var stale = await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1)).ExpectProblem(HttpStatusCode.Conflict, "stale_recipient_key");
        Assert.Equal("La llave del otro cambió. Actualízala y reenvía.", stale.GetProperty("title").GetString());
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 2)).Expect(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Llaves_faltantes_dan_409_con_su_codigo()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        var matchId = await CreateMatchAsync(api, a, b);

        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1)).ExpectProblem(HttpStatusCode.Conflict, "unknown_sender_key");
        await PutKeysAsync(a);
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1)).ExpectProblem(HttpStatusCode.Conflict, "peer_keys_missing");
        await PutKeysAsync(b);
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 7, 1)).ExpectProblem(HttpStatusCode.Conflict, "unknown_sender_key");
    }

    [Fact]
    public async Task Match_cerrado_da_403_al_enviar_pero_permite_leer_historial()
    {
        var (a, b, matchId) = await PairAsync(api);
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1)).Expect(HttpStatusCode.Created);
        await api.WithDbAsync(db => db.Matches.Where(m => m.Id == matchId).ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MatchStatus.Closed), Ct));

        await SendAsync(b, matchId, MessageBody(Guid.NewGuid(), 1, 1)).ExpectProblem(HttpStatusCode.Forbidden, "match_closed");
        var page = await b.Client.GetAsync(Messages(matchId), Ct).Expect<MessagePage>(HttpStatusCode.OK);
        Assert.Single(page.Items);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bloqueo_en_cualquier_direccion_da_403_blocked(bool aBlocksB)
    {
        var (a, b, matchId) = await PairAsync(api);
        await api.WithDbAsync(async db =>
        {
            db.Blocks.Add(aBlocksB ? new Block { BlockerId = a.Id, BlockedId = b.Id } : new Block { BlockerId = b.Id, BlockedId = a.Id });
            await db.SaveChangesAsync();
        });
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1)).ExpectProblem(HttpStatusCode.Forbidden, "blocked");
        await SendAsync(b, matchId, MessageBody(Guid.NewGuid(), 1, 1)).ExpectProblem(HttpStatusCode.Forbidden, "blocked");
    }

    [Fact]
    public async Task Un_tercero_no_puede_listar_enviar_ni_confirmar_en_chat_ajeno()
    {
        var (a, _, matchId) = await PairAsync(api);
        var c = await api.CreateUserAsync("C");
        await PutKeysAsync(c);
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1)).Expect(HttpStatusCode.Created);

        await c.Client.GetAsync(Messages(matchId), Ct).ExpectProblem(HttpStatusCode.NotFound, "match_not_found");
        await SendAsync(c, matchId, MessageBody(Guid.NewGuid(), 1, 1)).ExpectProblem(HttpStatusCode.NotFound, "match_not_found");
        await c.Client.PostJson(Messages(matchId) + "/ack", new { upToSeq = 1 }).ExpectProblem(HttpStatusCode.NotFound, "match_not_found");
        await api.WithDbAsync(async db => Assert.Equal(1, await db.ChatMessages.CountAsync(m => m.MatchId == matchId, Ct)));
    }

    [Fact]
    public async Task Entradas_invalidas_dan_400_nunca_500()
    {
        var (a, _, matchId) = await PairAsync(api);
        await SendAsync(a, matchId, MessageBody(Guid.Empty, 1, 1)).ExpectProblem(HttpStatusCode.BadRequest);
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 0, 1)).ExpectProblem(HttpStatusCode.BadRequest);
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1, nonce: Rand(12))).ExpectProblem(HttpStatusCode.BadRequest);
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1, ciphertext: Rand(16))).ExpectProblem(HttpStatusCode.BadRequest);
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1, ciphertext: Rand(65537))).ExpectProblem(HttpStatusCode.BadRequest);
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1, franking: Rand(31))).ExpectProblem(HttpStatusCode.BadRequest);
        await a.Client.PostJson(Messages(matchId) + "/ack", new { upToSeq = 0 }).ExpectProblem(HttpStatusCode.BadRequest);
        await a.Client.GetAsync(Messages(matchId) + "?before=abc", Ct).ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Paginacion_before_after_y_next_cursor()
    {
        var (a, _, matchId) = await PairAsync(api);
        for (var i = 0; i < 5; i++) await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1)).Expect(HttpStatusCode.Created);

        async Task<MessagePage> Page(string query) => await a.Client.GetAsync(Messages(matchId) + query, Ct).Expect<MessagePage>(HttpStatusCode.OK);

        var last = await Page("?limit=2");
        Assert.Equal([5L, 4L], last.Items.Select(m => m.Seq));
        Assert.Equal(4, last.NextCursor);

        var older = await Page("?before=4&limit=2");
        Assert.Equal([3L, 2L], older.Items.Select(m => m.Seq));
        Assert.Equal(2, older.NextCursor);
        var oldest = await Page("?before=2&limit=2");
        Assert.Equal([1L], oldest.Items.Select(m => m.Seq));
        Assert.Null(oldest.NextCursor);

        var newer = await Page("?after=0&limit=2");
        Assert.Equal([1L, 2L], newer.Items.Select(m => m.Seq));
        Assert.Equal(2, newer.NextCursor);
        var rest = await Page("?after=2&limit=10");
        Assert.Equal([3L, 4L, 5L], rest.Items.Select(m => m.Seq));
        Assert.Null(rest.NextCursor);
        Assert.Null((await Page("?limit=5")).NextCursor);

        await a.Client.GetAsync(Messages(matchId) + "?before=3&after=1", Ct).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await a.Client.GetAsync(Messages(matchId) + "?limit=0", Ct).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await a.Client.GetAsync(Messages(matchId) + "?limit=101", Ct).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Una_pagina_se_corta_en_1_MiB_aunque_no_llegue_a_limit()
    {
        var (a, _, matchId) = await PairAsync(api);
        for (var i = 0; i < 20; i++)
            await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1, ciphertext: Rand(65536))).Expect(HttpStatusCode.Created);

        var page = await a.Client.GetAsync(Messages(matchId) + "?after=0&limit=100", Ct).Expect<MessagePage>(HttpStatusCode.OK);
        Assert.Equal(16, page.Items.Count);   // 16 × 64 KiB = 1 MiB
        Assert.Equal(16, page.NextCursor);
        var rest = await a.Client.GetAsync(Messages(matchId) + "?after=16&limit=100", Ct).Expect<MessagePage>(HttpStatusCode.OK);
        Assert.Equal([17L, 18L, 19L, 20L], rest.Items.Select(m => m.Seq));
        Assert.Null(rest.NextCursor);
    }

    [Fact]
    public async Task Con_50_MiB_sin_recoger_el_remitente_recibe_429_hasta_que_el_otro_haga_ack()
    {
        var (a, b, matchId) = await PairAsync(api);
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1)).Expect(HttpStatusCode.Created);   // seq 1
        await api.WithDbAsync(async db =>
        {
            db.ChatMessages.Add(new ChatMessage
            {
                Id = Guid.NewGuid(), MatchId = matchId, SenderId = a.Id, Seq = 1000, Version = 1,
                Nonce = Rand(24), Ciphertext = new byte[ChatService.MaxPendingBytesPerSender - 100],
            });
            await db.SaveChangesAsync(Ct);
        });

        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1, ciphertext: Rand(200)))
            .ExpectProblem(HttpStatusCode.TooManyRequests, "chat_quota_exceeded");
        // La cuota es por remitente: el otro sigue escribiendo.
        await SendAsync(b, matchId, MessageBody(Guid.NewGuid(), 1, 1)).Expect(HttpStatusCode.Created);

        await b.Client.PostJson(Messages(matchId) + "/ack", new { upToSeq = 1000 }).Expect(HttpStatusCode.NoContent);
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1, ciphertext: Rand(200))).Expect(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Ack_borra_solo_lo_recibido_no_lo_enviado_por_mi()
    {
        var (a, b, matchId) = await PairAsync(api);
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1)).Expect(HttpStatusCode.Created); // 1
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1)).Expect(HttpStatusCode.Created); // 2
        await SendAsync(b, matchId, MessageBody(Guid.NewGuid(), 1, 1)).Expect(HttpStatusCode.Created); // 3
        await SendAsync(a, matchId, MessageBody(Guid.NewGuid(), 1, 1)).Expect(HttpStatusCode.Created); // 4

        await b.Client.PostJson(Messages(matchId) + "/ack", new { upToSeq = 3 }).Expect(HttpStatusCode.NoContent);
        var page = await b.Client.GetAsync(Messages(matchId) + "?after=0", Ct).Expect<MessagePage>(HttpStatusCode.OK);
        Assert.Equal([3L, 4L], page.Items.Select(m => m.Seq));

        await a.Client.PostJson(Messages(matchId) + "/ack", new { upToSeq = 100 }).Expect(HttpStatusCode.NoContent);
        var left = await a.Client.GetAsync(Messages(matchId) + "?after=0", Ct).Expect<MessagePage>(HttpStatusCode.OK);
        Assert.Equal([4L], left.Items.Select(m => m.Seq));
    }

    [Fact]
    public async Task Message_available_llega_al_peer_por_signalr()
    {
        var (a, b, matchId) = await PairAsync(api);
        var got = new TaskCompletionSource<MessageAvailableEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hub = Hub(api, b.AccessToken);
        hub.On<MessageAvailableEvent>("MessageAvailable", e => got.TrySetResult(e));
        await hub.StartAsync(Ct);

        var id = Guid.NewGuid();
        var accepted = await SendAsync(a, matchId, MessageBody(id, 1, 1)).Expect<MessageAcceptedDto>(HttpStatusCode.Created);
        var ev = await Within(got);
        Assert.Equal(new MessageAvailableEvent(matchId, id, accepted.Seq), ev);
    }

    [Fact]
    public async Task Hub_sin_token_falla_con_401()
    {
        await using var hub = Hub(api, null);
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => hub.StartAsync(Ct));
        Assert.Contains("401", ex.Message);
    }
}
