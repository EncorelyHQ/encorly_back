using Confluent.Kafka;
using EncorelyApplication.Interfaces;
using EncorelyDomain.Events;
using EncorelyModels;
using EncorelyQuery.Interfaces;
using EncorelyRepository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EncorelyWorker;

/// <summary>
/// Consume el topic `swipe-raw-events` y, por cada Like, intenta crear matches
/// con candidatos compatibles (afinidad >= umbral). El conteo de swipes ya lo
/// hace SwipeService de forma síncrona — el worker NO lo incrementa.
/// </summary>
public class KafkaConsumerWorker : BackgroundService
{
    /// <summary>Mínimo de swipes para empezar a generar matches (umbral del ADN musical).</summary>
    private const int MinSwipesToMatch = 25;
    /// <summary>Cuántos candidatos top consideramos por evento de swipe.</summary>
    private const int MaxMatchesPerSwipe = 1;

    private readonly ILogger<KafkaConsumerWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;

    public KafkaConsumerWorker(
        ILogger<KafkaConsumerWorker> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrap = _configuration["Kafka:BootstrapServers"]
            ?? $"{Environment.GetEnvironmentVariable("KAFKA_HOST") ?? "localhost"}:{Environment.GetEnvironmentVariable("KAFKA_PORT") ?? "9092"}";

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = "encorely-processor-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(KafkaTopics.SwipeRawEvents);

        _logger.LogInformation("Encorely Worker started. Bootstrap={Bootstrap}. Listening for '{Topic}'...",
            bootstrap, KafkaTopics.SwipeRawEvents);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<Ignore, string>? consumeResult = null;
                try
                {
                    consumeResult = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning(ex, "[WORKER] Consume error, continuing");
                    continue;
                }

                if (consumeResult?.Message?.Value is null) continue;

                SwipeRegisteredEvent? evt;
                try
                {
                    evt = JsonSerializer.Deserialize<SwipeRegisteredEvent>(consumeResult.Message.Value);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "[WORKER] Mensaje no deserializable, ignorando: {Raw}", consumeResult.Message.Value);
                    continue;
                }

                if (evt is null) continue;

                try
                {
                    await ProcessSwipeAsync(evt, stoppingToken);
                }
                catch (Exception ex)
                {
                    // No queremos que un error en un mensaje tumbe el consumer.
                    _logger.LogError(ex, "[WORKER] Error procesando swipe de {UserId}", evt.UserId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown limpio
        }
        finally
        {
            try { consumer.Close(); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Lógica de match-generation: si el usuario ya tiene su umbral de swipes
    /// y este swipe fue un Like, busca candidatos compatibles y crea Match(es)
    /// con los que aún no estén emparejados.
    /// </summary>
    private async Task ProcessSwipeAsync(SwipeRegisteredEvent evt, CancellationToken ct)
    {
        // Solo procesamos Like (= "Right" en el enum) — los Dislike no generan matches.
        if (!string.Equals(evt.Direction, nameof(SwipeDirection.Right), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("[WORKER] Swipe no-Like de {UserId} ignorado para matching", evt.UserId);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var usuarioQueries = sp.GetRequiredService<IUsuarioQueries>();
        var profileQueries = sp.GetRequiredService<IMusicalProfileQueries>();
        var matchQueries = sp.GetRequiredService<IMatchQueries>();
        var matchRepository = sp.GetRequiredService<IMatchRepository>();
        var compatibility = sp.GetRequiredService<ICompatibilityService>();

        var me = await usuarioQueries.GetByIdAsync(evt.UserId);
        if (me is null)
        {
            _logger.LogWarning("[WORKER] Usuario {UserId} no encontrado", evt.UserId);
            return;
        }

        if (me.SwipeCount < MinSwipesToMatch)
        {
            _logger.LogDebug("[WORKER] Usuario {UserId} con {Count} swipes, aún bajo umbral ({Min})",
                me.Id, me.SwipeCount, MinSwipesToMatch);
            return;
        }

        var myProfile = await profileQueries.GetByUserIdAsync(me.Id);
        if (myProfile is null)
        {
            _logger.LogInformation("[WORKER] Usuario {UserId} sin perfil musical aún, omitiendo matching", me.Id);
            return;
        }

        // Universo de candidatos: usuarios distintos a mí. Filtramos los ya emparejados.
        var existingMatches = await matchQueries.GetByUserIdAsync(me.Id);
        var alreadyMatchedIds = new HashSet<Guid>(existingMatches.Select(m =>
            m.UserId1 == me.Id ? m.UserId2 : m.UserId1));

        var allUsers = await usuarioQueries.GetAllAsync();
        var candidates = new List<(Usuario user, double affinity, bool highPriority)>();

        foreach (var candidate in allUsers)
        {
            if (candidate.Id == me.Id) continue;
            if (alreadyMatchedIds.Contains(candidate.Id)) continue;

            var profile = await profileQueries.GetByUserIdAsync(candidate.Id);
            if (profile is null) continue;

            var affinity = compatibility.CalculateAffinity(myProfile, profile);
            if (!compatibility.IsCompatible(affinity)) continue;

            candidates.Add((candidate, affinity, affinity >= 85.0));
        }

        if (candidates.Count == 0)
        {
            _logger.LogInformation("[WORKER] Sin candidatos compatibles nuevos para {UserId}", me.Id);
            return;
        }

        var top = candidates
            .OrderByDescending(c => c.affinity)
            .Take(MaxMatchesPerSwipe)
            .ToList();

        foreach (var (candidate, affinity, highPriority) in top)
        {
            var match = new Match
            {
                Id = Guid.NewGuid(),
                UserId1 = me.Id,
                UserId2 = candidate.Id,
                AffinityScore = affinity,
                IsHighPriority = highPriority,
                CreatedAt = DateTime.UtcNow,
            };

            await matchRepository.CreateAsync(match);
            _logger.LogInformation(
                "[MATCH_CREATED] {UserA} ↔ {UserB} affinity={Affinity:F1} high={High} matchId={MatchId}",
                me.Id, candidate.Id, affinity, highPriority, match.Id);
        }
    }
}
