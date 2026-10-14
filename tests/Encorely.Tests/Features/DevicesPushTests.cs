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

        // Mismo teléfono, otra cuenta: el token pasa a B.
        await b.Client.PutJson("/api/v1/devices/push-token", new { token, platform = "android" }).Expect(HttpStatusCode.NoContent);
        Assert.Equal(b.Id.ToString(), await OwnerOf(token));
        await a.Client.SendAsync(Delete(token), Ct).Expect(HttpStatusCode.NoContent);
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

    private PushDispatcher NewDispatcher(PushQueue queue, FakeExpo expo, string? accessToken = null)
        => new(queue, api.Service<IServiceScopeFactory>(), new FakeFactory(expo),
            Options.Create(new PushOptions { Enabled = true, ExpoAccessToken = accessToken, ExpoEndpoint = "https://expo.invalid/push" }),
            NullLogger<PushDispatcher>.Instance);

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
                Assert.Equal("newMessage", m.GetProperty("data").GetProperty("type").GetString());
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
