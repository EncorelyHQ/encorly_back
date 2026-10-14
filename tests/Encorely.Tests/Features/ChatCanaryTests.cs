using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Encorely.Api.Features.Chat;
using Encorely.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Npgsql;
using static Encorely.Tests.Features.ChatKit;

namespace Encorely.Tests.Features;

/// <summary>B9: el texto plano nunca toca el servidor, ni en tablas ni en logs.</summary>
public sealed class ChatCanaryTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public List<string> Lines { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);
        public void Dispose() { }

        private sealed class CaptureLogger(CaptureLoggerProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var structured = state is IEnumerable<KeyValuePair<string, object?>> kv ? string.Join(' ', kv.Select(p => $"{p.Key}={p.Value}")) : "";
                lock (owner.Lines) owner.Lines.Add($"{category} {formatter(state, exception)} {structured} {exception}");
            }
        }
    }

    [Fact]
    public async Task El_texto_canario_no_aparece_en_ninguna_tabla_ni_en_los_logs()
    {
        var (a, b, matchId) = await PairAsync(api);
        var canary = $"CANARIO-{Guid.NewGuid()}";
        var plaintext = Encoding.UTF8.GetBytes(canary);

        // El cliente cifra: ChaCha20-Poly1305 de .NET con nonce de 24 B (los 12 primeros alimentan el cifrador).
        var nonce = Rand(24);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aead = new ChaCha20Poly1305(Rand(32)))
            aead.Encrypt(nonce.AsSpan(0, 12), plaintext, cipher, tag);
        var ciphertext = cipher.Concat(tag).ToArray();

        var logs = new CaptureLoggerProvider();
        await using var factory = api.Factory.WithWebHostBuilder(w => w.ConfigureLogging(l => { l.SetMinimumLevel(LogLevel.Trace); l.AddProvider(logs); }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", a.AccessToken);
        await client.PostJson($"/api/v1/matches/{matchId}/messages", MessageBody(Guid.NewGuid(), 1, 1, ciphertext, nonce)).Expect(HttpStatusCode.Created);
        // Reintento y listado también pasan por el servidor.
        await b.Client.GetAsync($"/api/v1/matches/{matchId}/messages", Ct).Expect(HttpStatusCode.OK);

        var needles = new[]
        {
            canary,
            Convert.ToBase64String(plaintext),
            Convert.ToHexString(plaintext),
            Convert.ToHexString(plaintext).ToLowerInvariant(),
        };

        await using var conn = new NpgsqlConnection(api.ConnectionString);
        await conn.OpenAsync(Ct);
        var tables = new List<string>();
        await using (var cmd = new NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'", conn))
        await using (var reader = await cmd.ExecuteReaderAsync(Ct))
            while (await reader.ReadAsync(Ct)) tables.Add(reader.GetString(0));
        Assert.Contains("chat_messages", tables);

        foreach (var table in tables)
        {
            await using var scan = new NpgsqlCommand($"SELECT t::text FROM public.\"{table}\" t", conn);
            await using var rows = await scan.ExecuteReaderAsync(Ct);
            while (await rows.ReadAsync(Ct))
            {
                var row = rows.GetString(0);
                foreach (var needle in needles) Assert.False(row.Contains(needle, StringComparison.Ordinal), $"El canario apareció en la tabla {table}");
            }
        }

        string[] captured;
        lock (logs.Lines) captured = logs.Lines.ToArray();
        Assert.NotEmpty(captured);
        foreach (var line in captured)
            foreach (var needle in needles) Assert.False(line.Contains(needle, StringComparison.Ordinal), "El canario apareció en los logs");
    }
}
