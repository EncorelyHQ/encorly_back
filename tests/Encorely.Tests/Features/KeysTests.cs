using Microsoft.AspNetCore.SignalR.Client;
using System.Net;
using Encorely.Api.Features.Chat;
using Encorely.Api.Infrastructure.Realtime;
using Encorely.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static Encorely.Tests.Features.ChatKit;

namespace Encorely.Tests.Features;

public sealed class KeysTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Put_publica_y_get_me_devuelve_lo_mismo()
    {
        var u = await api.CreateUserAsync();
        var body = KeysBody(1);
        var put = await u.Client.PutJson("/api/v1/keys/me", body).Expect<KeyBundleDto>(HttpStatusCode.OK);
        var mine = await u.Client.GetAsync("/api/v1/keys/me", Ct).Expect<KeyBundleDto>(HttpStatusCode.OK);
        Assert.Equal(u.Id, put.UserId);
        Assert.Equal(1, mine.BoxKeyId);
        Assert.Equal(put.BoxKey, mine.BoxKey);
        Assert.Equal(put.IdentityKey, mine.IdentityKey);
    }

    [Fact]
    public async Task Get_me_sin_llaves_da_404_keys_missing()
    {
        var u = await api.CreateUserAsync();
        await u.Client.GetAsync("/api/v1/keys/me", Ct).ExpectProblem(HttpStatusCode.NotFound, "keys_missing");
    }

    [Fact]
    public async Task Tamanos_invalidos_dan_400()
    {
        var u = await api.CreateUserAsync();
        await u.Client.PutJson("/api/v1/keys/me", KeysBody(1, identity: Rand(31))).ExpectProblem(HttpStatusCode.BadRequest);
        await u.Client.PutJson("/api/v1/keys/me", KeysBody(1, box: Rand(33))).ExpectProblem(HttpStatusCode.BadRequest);
        await u.Client.PutJson("/api/v1/keys/me", KeysBody(1, sig: Rand(63))).ExpectProblem(HttpStatusCode.BadRequest);
        await u.Client.PutJson("/api/v1/keys/me", KeysBody(0)).ExpectProblem(HttpStatusCode.BadRequest);
        await u.Client.GetAsync("/api/v1/keys/me", Ct).ExpectProblem(HttpStatusCode.NotFound, "keys_missing");
    }

    [Fact]
    public async Task BoxKeyId_no_mayor_da_409_stale_y_mayor_rota()
    {
        var u = await api.CreateUserAsync();
        await PutKeysAsync(u, 5);

        var conflict = await u.Client.PutJson("/api/v1/keys/me", KeysBody(5)).ExpectProblem(HttpStatusCode.Conflict, "stale_box_key_id");
        Assert.Contains("5", conflict.GetProperty("detail").GetString());
        await u.Client.PutJson("/api/v1/keys/me", KeysBody(3)).ExpectProblem(HttpStatusCode.Conflict, "stale_box_key_id");

        var rotated = await PutKeysAsync(u, 6);
        Assert.Equal(6, rotated.BoxKeyId);
        // El historial incluye todas las rotaciones: bajar de nuevo sigue siendo 409.
        await u.Client.PutJson("/api/v1/keys/me", KeysBody(6)).ExpectProblem(HttpStatusCode.Conflict, "stale_box_key_id");
        await api.WithDbAsync(async db => Assert.Equal(2, await db.UserBoxKeys.CountAsync(k => k.UserId == u.Id, Ct)));
    }

    [Fact]
    public async Task Put_simultaneo_con_el_mismo_id_da_un_200_y_un_409()
    {
        var u = await api.CreateUserAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => u.Client.PutJson("/api/v1/keys/me", KeysBody(1))));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Re_put_identico_da_200_sin_cambios_ni_evento_y_la_rotacion_avisa_al_peer()
    {
        var (a, b, matchId) = await PairAsync(api);
        var events = new List<PeerKeyChangedEvent>();
        var rotation = new TaskCompletionSource<PeerKeyChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hub = Hub(api, b.AccessToken);
        hub.On<PeerKeyChangedEvent>("PeerKeyChanged", e => { lock (events) events.Add(e); rotation.TrySetResult(e); });
        await hub.StartAsync(Ct);

        var current = await a.Client.GetAsync("/api/v1/keys/me", Ct).Expect<KeyBundleDto>(HttpStatusCode.OK);
        var same = KeysBody(current.BoxKeyId, current.IdentityKey, current.BoxKey, current.BoxKeySig);
        var again = await a.Client.PutJson("/api/v1/keys/me", same).Expect<KeyBundleDto>(HttpStatusCode.OK);
        Assert.Equal(current.UpdatedAt, again.UpdatedAt);

        await PutKeysAsync(a, 2);
        var ev = await Within(rotation);
        Assert.Equal(matchId, ev.MatchId);
        Assert.Equal(a.Id, ev.UserId);
        await Task.Delay(300, Ct);
        lock (events) Assert.Single(events);
    }

    [Fact]
    public async Task Peer_keys_solo_para_miembros_y_404_peer_keys_missing()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        var c = await api.CreateUserAsync();
        await PutKeysAsync(a);
        var matchId = await CreateMatchAsync(api, a, b);

        await a.Client.GetAsync($"/api/v1/matches/{matchId}/peer-keys", Ct).ExpectProblem(HttpStatusCode.NotFound, "peer_keys_missing");
        await PutKeysAsync(b);
        var seen = await a.Client.GetAsync($"/api/v1/matches/{matchId}/peer-keys", Ct).Expect<KeyBundleDto>(HttpStatusCode.OK);
        Assert.Equal(b.Id, seen.UserId);

        await c.Client.GetAsync($"/api/v1/matches/{matchId}/peer-keys", Ct).ExpectProblem(HttpStatusCode.NotFound, "match_not_found");
        await c.Client.GetAsync($"/api/v1/matches/{matchId}/peer-keys/box/1", Ct).ExpectProblem(HttpStatusCode.NotFound, "match_not_found");
        await a.Client.GetAsync($"/api/v1/matches/{Guid.NewGuid()}/peer-keys", Ct).ExpectProblem(HttpStatusCode.NotFound, "match_not_found");
    }

    [Fact]
    public async Task Historial_por_boxKeyId_conserva_las_llaves_viejas()
    {
        var (a, b, matchId) = await PairAsync(api);
        var first = await b.Client.GetAsync("/api/v1/keys/me", Ct).Expect<KeyBundleDto>(HttpStatusCode.OK);
        await PutKeysAsync(b, 2);

        var old = await a.Client.GetAsync($"/api/v1/matches/{matchId}/peer-keys/box/1", Ct).Expect<BoxKeyDto>(HttpStatusCode.OK);
        Assert.Equal(first.BoxKey, old.BoxKey);
        Assert.Equal(b.Id, old.UserId);
        var now = await a.Client.GetAsync($"/api/v1/matches/{matchId}/peer-keys", Ct).Expect<KeyBundleDto>(HttpStatusCode.OK);
        Assert.Equal(2, now.BoxKeyId);
        await a.Client.GetAsync($"/api/v1/matches/{matchId}/peer-keys/box/99", Ct).ExpectProblem(HttpStatusCode.NotFound);
    }
}
