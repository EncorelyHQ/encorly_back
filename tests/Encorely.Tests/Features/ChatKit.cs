using System.Net;
using System.Security.Cryptography;
using Encorely.Api.Features.Chat;
using Encorely.Api.Features.Matching;
using Encorely.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Encorely.Tests.Features;

/// <summary>Utilidades compartidas de los tests de llaves, chat y push. Los datos cifrados son aleatorios: el servidor no los interpreta.</summary>
public static class ChatKit
{
    public static byte[] Rand(int n) => RandomNumberGenerator.GetBytes(n);

    public static object KeysBody(int boxKeyId, byte[]? identity = null, byte[]? box = null, byte[]? sig = null)
        => new { identityKey = identity ?? Rand(32), boxKey = box ?? Rand(32), boxKeyId, boxKeySig = sig ?? Rand(64) };

    public static async Task<KeyBundleDto> PutKeysAsync(TestUser user, int boxKeyId = 1)
        => await user.Client.PutJson("/api/v1/keys/me", KeysBody(boxKeyId)).Expect<KeyBundleDto>(HttpStatusCode.OK);

    public static object MessageBody(Guid id, int senderBoxKeyId, int recipientBoxKeyId, byte[]? ciphertext = null, byte[]? nonce = null, byte[]? franking = null)
        => new
        {
            id,
            version = 1,
            senderBoxKeyId,
            recipientBoxKeyId,
            nonce = nonce ?? Rand(24),
            ciphertext = ciphertext ?? Rand(48),
            frankingTag = franking,
        };

    public static Task<HttpResponseMessage> SendAsync(TestUser from, Guid matchId, object body)
        => from.Client.PostJson($"/api/v1/matches/{matchId}/messages", body);

    public static async Task<Guid> CreateMatchAsync(ApiFixture api, TestUser x, TestUser y, MatchStatus status = MatchStatus.Active)
    {
        var (a, b) = Match.OrderPair(x.Id, y.Id);
        var match = new Match { UserAId = a, UserBId = b, Status = status };
        if (status == MatchStatus.Closed) { match.ClosedAt = DateTimeOffset.UtcNow; match.CloseReason = MatchCloseReason.Unmatched; }
        await api.WithDbAsync(async db => { db.Matches.Add(match); await db.SaveChangesAsync(); });
        return match.Id;
    }

    /// <summary>Dos usuarios con match activo y llaves publicadas (boxKeyId 1 cada uno).</summary>
    public static async Task<(TestUser A, TestUser B, Guid MatchId)> PairAsync(ApiFixture api)
    {
        var a = await api.CreateUserAsync("A");
        var b = await api.CreateUserAsync("B");
        await PutKeysAsync(a);
        await PutKeysAsync(b);
        return (a, b, await CreateMatchAsync(api, a, b));
    }

    public static HubConnection Hub(ApiFixture api, string? token)
        => new HubConnectionBuilder()
            .WithUrl(new Uri(api.Factory.Server.BaseAddress, "/hubs/chat"), o =>
            {
                o.HttpMessageHandlerFactory = _ => api.Factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                if (token is not null) o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    public static Task<T> Within<T>(TaskCompletionSource<T> tcs) => tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
}
