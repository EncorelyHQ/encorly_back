using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Encorely.Api.Features.Devices;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Push;
using Encorely.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Encorely.Tests.Features;

public sealed class DevicesPushTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewToken() => $"ExponentPushToken[{Guid.NewGuid():N}]";

    private Task<string?> OwnerOf(string token) => api.WithDbAsync(async db =>
        (Guid?)(await db.DevicePushTokens.AsNoTracking().Where(t => t.Token == token).Select(t => (Guid?)t.UserId).SingleOrDefaultAsync(Ct)) is { } id ? id.ToString() : null);

    [Fact]
    public async Task Registrar_reasignar_y_borrar_solo_el_mio()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        var token = NewToken();

        await a.Client.PutJson("/api/v1/devices/push-token", new { token, platform = "android" }).Expect(HttpStatusCode.NoContent);
        Assert.Equal(a.Id.ToString(), await OwnerOf(token));
        // Idempotente: el mismo usuario actualiza plataforma.
        await a.Client.PutJson("/api/v1/devices/push-token", new { token, platform = "ios" }).Expect(HttpStatusCode.NoContent);
        await api.WithDbAsync(async db => Assert.Equal(DevicePlatform.Ios, (await db.DevicePushTokens.SingleAsync(t => t.Token == token, Ct)).Platform));

        // B no puede borrar el token de A: 204 y sigue siendo de A.
        await b.Client.SendAsync(Delete(token), Ct).Expect(HttpStatusCode.NoContent);
        Assert.Equal(a.Id.ToString(), await OwnerOf(token));

        // Mismo teléfono, otra cuenta: el token pasa a B y la sesión de A en ese teléfono se cierra.
        await b.Client.PutJson("/api/v1/devices/push-token", new { token, platform = "android" }).Expect(HttpStatusCode.NoContent);
        Assert.Equal(b.Id.ToString(), await OwnerOf(token));
        await a.Client.SendAsync(Delete(token), Ct).ExpectProblem(HttpStatusCode.Unauthorized);
        Assert.Equal(b.Id.ToString(), await OwnerOf(token));

        await b.Client.SendAsync(Delete(token), Ct).Expect(HttpStatusCode.NoContent);
        Assert.Null(await OwnerOf(token));
        await b.Client.SendAsync(Delete(token), Ct).Expect(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Entradas_invalidas_dan_400_nunca_500()
    {
        var u = await api.CreateUserAsync();
        await u.Client.PutJson("/api/v1/devices/push-token", new { token = "", platform = "android" }).ExpectProblem(HttpStatusCode.BadRequest);
        await u.Client.PutJson("/api/v1/devices/push-token", new { token = new string('x', 201), platform = "android" }).ExpectProblem(HttpStatusCode.BadRequest);
        await u.Client.PutJson("/api/v1/devices/push-token", new { token = NewToken(), platform = "symbian" }).ExpectProblem(HttpStatusCode.BadRequest);
        foreach (var token in new[] { "fcm-crudo-123", "ExponentPushToken[]", "ExponentPushToken[a b]", "ExpoPushToken[abc]x" })
            await u.Client.PutJson("/api/v1/devices/push-token", new { token, platform = "android" }).ExpectProblem(HttpStatusCode.BadRequest);
        await u.Client.PutJson("/api/v1/devices/push-token", new { token = "ExpoPushToken[aB3_-x]", platform = "ios" }).Expect(HttpStatusCode.NoContent);
        await u.Client.SendAsync(Delete(new string('x', 201)), Ct).ExpectProblem(HttpStatusCode.BadRequest);
    }

    private static HttpRequestMessage Delete(string token)
        => new(HttpMethod.Delete, "/api/v1/devices/push-token") { Content = JsonContent(new { token }) };

    private static StringContent JsonContent(object body)
        => new(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

    private sealed class FakeExpo(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Body, AuthenticationHeaderValue? Auth, Uri? Uri)> Calls { get; } = [];
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            lock (Calls) Calls.Add((body, request.Headers.Authorization, request.RequestUri));
            var response = respond(body);
            Called.TrySetResult();
            return response;
        }
    }

    private sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("expo-push", name);
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private PushDispatcher NewDispatcher(PushQueue queue, FakeExpo expo, string? accessToken = null, PushReceiptTracker? tracker = null)
        => new(queue, tracker ?? new PushReceiptTracker(), api.Service<IServiceScopeFactory>(), new FakeFactory(expo),
            Options.Create(new PushOptions { Enabled = true, ExpoAccessToken = accessToken, ExpoEndpoint = "https://expo.invalid/push" }),
            TimeProvider.System, NullLogger<PushDispatcher>.Instance);

    private PushReceiptChecker NewChecker(PushReceiptTracker tracker, FakeExpo expo)
        => new(tracker, api.Service<IServiceScopeFactory>(), new FakeFactory(expo),
            Options.Create(new PushOptions { Enabled = true, ExpoEndpoint = "https://expo.invalid/--/api/v2/push/send" }),
            TimeProvider.System, NullLogger<PushReceiptChecker>.Instance);

    private static HttpResponseMessage Tickets(params string?[] errors)
    {
        var data = errors.Select(e => e is null ? (object)new { status = "ok", id = Guid.NewGuid() } : new { status = "error", message = "x", details = new { error = e } });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(new { data }) };
    }

    [Fact]
    public async Task Push_sin_contenido_viaja_por_la_cola_y_DeviceNotRegistered_borra_el_token()
    {
        var sender = await api.CreateUserAsync($"Remitente-{Guid.NewGuid():N}");
        var target = await api.CreateUserAsync();
        var good = NewToken();
        var dead = NewToken();
        foreach (var t in new[] { good, dead })
            await target.Client.PutJson("/api/v1/devices/push-token", new { token = t, platform = "android" }).Expect(HttpStatusCode.NoContent);

        var expo = new FakeExpo(body =>
        {
            // Los tickets van en el orden de los mensajes enviados.
            var order = JsonDocument.Parse(body).RootElement.EnumerateArray().Select(m => m.GetProperty("to").GetString()).ToList();
            return Tickets(order.Select(t => t == dead ? "DeviceNotRegistered" : null).ToArray());
        });
        var queue = new PushQueue();
        var dispatcher = NewDispatcher(queue, expo, accessToken: "token-de-prueba");
        await dispatcher.StartAsync(Ct);
        try
        {
            var matchId = Guid.NewGuid();
            await new ExpoPushSender(queue, NullLogger<ExpoPushSender>.Instance).SendAsync(target.Id, new PushNotification(PushKind.NewMessage, matchId), Ct);
            await expo.Called.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

            // El borrado ocurre después de la respuesta: se espera a que se refleje.
            for (var i = 0; i < 50 && await OwnerOf(dead) is not null; i++) await Task.Delay(100, Ct);

            var call = Assert.Single(expo.Calls);
            Assert.Equal("Bearer", call.Auth?.Scheme);
            var messages = JsonDocument.Parse(call.Body).RootElement.EnumerateArray().ToList();
            Assert.Equal(2, messages.Count);
            foreach (var m in messages)
            {
                Assert.Equal("Encorely", m.GetProperty("title").GetString());
                Assert.Equal("Tienes mensajes nuevos", m.GetProperty("body").GetString());
                Assert.Equal("chat", m.GetProperty("data").GetProperty("t").GetString());
                Assert.False(m.GetProperty("data").TryGetProperty("type", out _));
                Assert.Equal("messages", m.GetProperty("channelId").GetString());
                Assert.Equal(matchId, m.GetProperty("data").GetProperty("matchId").GetGuid());
                Assert.Equal("default", m.GetProperty("sound").GetString());
                Assert.Equal("high", m.GetProperty("priority").GetString());
            }
            Assert.DoesNotContain(sender.DisplayName, call.Body);
            Assert.DoesNotContain(target.DisplayName + "-", call.Body);

            Assert.Equal(target.Id.ToString(), await OwnerOf(good));
            Assert.Null(await OwnerOf(dead));
        }
        finally { await dispatcher.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task Sin_access_token_no_manda_Authorization_y_el_match_nuevo_tiene_su_texto()
    {
        var target = await api.CreateUserAsync();
        await target.Client.PutJson("/api/v1/devices/push-token", new { token = NewToken(), platform = "ios" }).Expect(HttpStatusCode.NoContent);
        var expo = new FakeExpo(_ => Tickets((string?)null));
        var dispatcher = NewDispatcher(new PushQueue(), expo);

        await dispatcher.ProcessAsync(new QueuedPush(target.Id, new PushNotification(PushKind.NewMatch, Guid.NewGuid())), Ct);

        var call = Assert.Single(expo.Calls);
        Assert.Null(call.Auth);
        Assert.Equal("https://expo.invalid/push", call.Uri?.ToString());
        Assert.Contains("Tienes un match nuevo", call.Body);
        var message = JsonDocument.Parse(call.Body).RootElement[0];
        Assert.Equal("match", message.GetProperty("data").GetProperty("t").GetString());
        Assert.Equal("messages", message.GetProperty("channelId").GetString());
    }

    [Fact]
    public async Task Receipt_DeviceNotRegistered_borra_el_token_y_los_pendientes_se_conservan()
    {
        var target = await api.CreateUserAsync();
        var dead = NewToken();
        var alive = NewToken();
        var waiting = NewToken();
        foreach (var t in new[] { dead, alive, waiting })
            await target.Client.PutJson("/api/v1/devices/push-token", new { token = t, platform = "android" }).Expect(HttpStatusCode.NoContent);

        var ids = new Dictionary<string, string> { [dead] = $"id-{Guid.NewGuid():N}", [alive] = $"id-{Guid.NewGuid():N}", [waiting] = $"id-{Guid.NewGuid():N}" };
        var tracker = new PushReceiptTracker();
        foreach (var (token, id) in ids) tracker.Add(id, token, DateTimeOffset.UtcNow);

        var expo = new FakeExpo(body =>
        {
            var asked = JsonDocument.Parse(body).RootElement.GetProperty("ids").EnumerateArray().Select(i => i.GetString()!).ToHashSet();
            Assert.Equal(ids.Values.ToHashSet(), asked);
            // "waiting" aún no tiene receipt: Expo simplemente no lo incluye.
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(new Dictionary<string, object>
                {
                    ["data"] = new Dictionary<string, object>
                    {
                        [ids[dead]] = new { status = "error", message = "x", details = new { error = "DeviceNotRegistered" } },
                        [ids[alive]] = new { status = "ok" },
                    },
                }),
            };
        });

        await NewChecker(tracker, expo).CheckAsync(Ct);

        var call = Assert.Single(expo.Calls);
        Assert.Equal("https://expo.invalid/--/api/v2/push/getReceipts", call.Uri?.ToString());
        Assert.Null(await OwnerOf(dead));
        Assert.Equal(target.Id.ToString(), await OwnerOf(alive));
        Assert.Equal(target.Id.ToString(), await OwnerOf(waiting));
        Assert.Equal(ids[waiting], Assert.Single(tracker.Snapshot(DateTimeOffset.UtcNow)).TicketId);
    }

    [Fact]
    public async Task Los_tickets_ok_del_envio_quedan_pendientes_de_receipt()
    {
        var target = await api.CreateUserAsync();
        var token = NewToken();
        await target.Client.PutJson("/api/v1/devices/push-token", new { token, platform = "ios" }).Expect(HttpStatusCode.NoContent);
        var tracker = new PushReceiptTracker();

        await NewDispatcher(new PushQueue(), new FakeExpo(_ => Tickets((string?)null)), tracker: tracker)
            .ProcessAsync(new QueuedPush(target.Id, new PushNotification(PushKind.NewMessage, Guid.NewGuid())), Ct);

        Assert.Equal(token, Assert.Single(tracker.Snapshot(DateTimeOffset.UtcNow)).Token);
    }

    [Fact]
    public async Task Receipts_se_consultan_en_lotes_de_mil_y_un_error_de_Expo_no_pierde_los_tickets()
    {
        var tracker = new PushReceiptTracker();
        for (var i = 0; i < 1500; i++) tracker.Add($"id-{i}", $"ExponentPushToken[{i}]", DateTimeOffset.UtcNow);
        var expo = new FakeExpo(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await NewChecker(tracker, expo).CheckAsync(Ct);

        Assert.Equal([1000, 500], expo.Calls.Select(c => JsonDocument.Parse(c.Body).RootElement.GetProperty("ids").GetArrayLength()).ToArray());
        Assert.Equal(1500, tracker.Count);
    }

    [Fact]
    public void Tracker_descarta_tickets_de_mas_de_24_horas_y_se_acota_a_10000()
    {
        var tracker = new PushReceiptTracker();
        var now = DateTimeOffset.UtcNow;
        tracker.Add("viejo", "t1", now - TimeSpan.FromHours(25));
        tracker.Add("reciente", "t2", now - TimeSpan.FromHours(1));

        Assert.Equal("reciente", Assert.Single(tracker.Snapshot(now)).TicketId);

        for (var i = 0; i < PushReceiptTracker.MaxPending + 5; i++) tracker.Add($"id-{i}", "t", now);
        Assert.Equal(PushReceiptTracker.MaxPending, tracker.Count);
    }

    [Fact]
    public async Task Error_de_Expo_no_lanza_ni_borra_tokens()
    {
        var target = await api.CreateUserAsync();
        var token = NewToken();
        await target.Client.PutJson("/api/v1/devices/push-token", new { token, platform = "ios" }).Expect(HttpStatusCode.NoContent);
        var expo = new FakeExpo(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await NewDispatcher(new PushQueue(), expo).ProcessAsync(new QueuedPush(target.Id, new PushNotification(PushKind.NewMessage, Guid.NewGuid())), Ct);

        Assert.Equal(target.Id.ToString(), await OwnerOf(token));
    }
}
