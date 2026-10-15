using System.Net;
using System.Text.Json;
using Encorely.Api.Features.Admin;
using Encorely.Api.Features.Users;
using Encorely.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Encorely.Tests.Features;

public sealed class ObservabilityTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class AccessLog : ILoggerProvider
    {
        public List<IReadOnlyDictionary<string, object?>> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);
        public void Dispose() { }

        private sealed class Logger(AccessLog owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (category != "Encorely.Http" || state is not IEnumerable<KeyValuePair<string, object?>> kv) return;
                lock (owner.Entries) owner.Entries.Add(kv.ToDictionary(p => p.Key, p => p.Value));
            }
        }
    }

    private sealed class ExplodingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("falla no controlada de prueba");
    }

    [Fact]
    public async Task Un_500_queda_como_500_en_el_log_de_acceso_con_el_traceId_que_recibe_el_cliente()
    {
        var admin = await api.CreateUserAsync("Admin", u => u.Role = UserRole.Admin);
        var log = new AccessLog();
        await using var factory = api.Factory.WithWebHostBuilder(w => w
            .ConfigureLogging(l => l.AddProvider(log))
            .ConfigureTestServices(s => s.AddHttpClient<ITunesClient>().ConfigurePrimaryHttpMessageHandler(() => new ExplodingHandler())));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);

        var res = await client.GetAsync("/api/v1/admin/itunes/search?term=bomba", Ct);
        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
        var traceId = JsonDocument.Parse(await res.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("traceId").GetString();

        IReadOnlyDictionary<string, object?> entry;
        lock (log.Entries) entry = Assert.Single(log.Entries, e => (string?)e["Path"] == "/api/v1/admin/itunes/search");
        Assert.Equal(500, entry["Status"]);
        Assert.Equal(traceId, entry["TraceId"]);
    }
}
