namespace Encorely.Api.Infrastructure.Configuration;

/// <summary>
/// Traduce las variables planas del <c>.env</c> (DB_HOST, JWT_SIGNING_KEY…) a las secciones de configuración.
/// Se agregan como fuente en memoria, así cualquier fuente posterior (tests) las puede sobrescribir.
/// </summary>
public static class EnvironmentMapping
{
    private static readonly (string Env, string Key)[] Map =
    [
        ("DB_HOST", "Database:Host"),
        ("DB_PORT", "Database:Port"),
        ("DB_NAME", "Database:Name"),
        ("DB_USER", "Database:User"),
        ("DB_PASSWORD", "Database:Password"),
        ("DB_SSL_MODE", "Database:SslMode"),
        ("DB_MIGRATE_ON_STARTUP", "Database:MigrateOnStartup"),
        ("SEED_CATALOG", "Seed:Catalog"),
        ("DEMO_USER_PASSWORD", "Seed:DemoUserPassword"),
        ("SEED_REVIEWER_ONLY", "Seed:ReviewerOnly"),
        ("JWT_SIGNING_KEY", "Jwt:SigningKey"),
        ("JWT_ISSUER", "Jwt:Issuer"),
        ("JWT_AUDIENCE", "Jwt:Audience"),
        ("JWT_ACCESS_TOKEN_MINUTES", "Jwt:AccessTokenMinutes"),
        ("JWT_REFRESH_TOKEN_DAYS", "Jwt:RefreshTokenDays"),
        ("RADAR_MIN_SWIPES", "Matching:RadarMinSwipes"),
        ("RATE_LIMIT_USER_WRITES_PER_MINUTE", "RateLimit:UserWritesPerMinute"),
        ("RATE_LIMIT_REPORTS_PER_HOUR", "RateLimit:ReportsPerHour"),
        ("RATE_LIMIT_TOKENS_PER_MINUTE", "RateLimit:TokenPermitPerMinute"),
        ("RADAR_MIN_AFFINITY", "Matching:RadarMinAffinity"),
        ("AFFINITY_MIN_SWIPES", "Matching:AffinityMinSwipes"),
        ("CHAT_MESSAGE_RETENTION_DAYS", "Chat:MessageRetentionDays"),
        ("PUSH_ENABLED", "Push:Enabled"),
        ("EXPO_PUSH_ACCESS_TOKEN", "Push:ExpoAccessToken"),
        ("CORS_ORIGINS", "Cors:Origins"),
        ("FORWARDED_TRUSTED_NETWORKS", "Proxy:TrustedNetworks"),
        ("PUBLIC_BASE_URL", "Legal:PublicBaseUrl"),
        ("PRIVACY_CONTACT_EMAIL", "Legal:PrivacyContactEmail"),
        ("SPOTIFY_CLIENT_ID", "Spotify:ClientId"),
        ("SPOTIFY_ALLOWED_REDIRECTS", "Spotify:AllowedRedirects"),
    ];

    public static IConfigurationBuilder AddEncorelyEnvironment(this IConfigurationBuilder builder)
    {
        var values = Map
            .Select(m => new KeyValuePair<string, string?>(m.Key, Environment.GetEnvironmentVariable(m.Env)))
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value));
        return builder.AddInMemoryCollection(values);
    }
}
