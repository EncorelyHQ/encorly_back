using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Encorely.Api.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    [Required] public string Host { get; set; } = "";
    [Range(1, 65535)] public int Port { get; set; } = 5432;
    [Required] public string Name { get; set; } = "";
    [Required] public string User { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    /// <summary>Disable | Allow | Prefer | Require | VerifyCA | VerifyFull. Local por defecto: Prefer.</summary>
    public SslMode SslMode { get; set; } = SslMode.Prefer;
    public bool MigrateOnStartup { get; set; }

    public string ConnectionString => new NpgsqlConnectionStringBuilder
    {
        Host = Host,
        Port = Port,
        Database = Name,
        Username = User,
        Password = Password,
        SslMode = SslMode,
    }.ConnectionString;
}

/// <summary>Fuera de Development/Testing una conexión a Postgres sin TLS exigido no es aceptable.</summary>
public sealed class DatabaseOptionsValidator(IHostEnvironment environment) : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing")) return ValidateOptionsResult.Success;
        return options.SslMode is SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"DB_SSL_MODE debe ser Require, VerifyCA o VerifyFull fuera de desarrollo (actual: {options.SslMode}): la conexión a la base de datos no puede ir sin TLS.");
    }
}

public sealed class ProxyOptions
{
    public const string Section = "Proxy";

    /// <summary>
    /// Redes CIDR (separadas por coma) de los proxies de confianza. Vacío = no se leen cabeceras X-Forwarded-*:
    /// cualquiera podría falsear su IP y esquivar el rate limit por IP.
    /// </summary>
    public string? TrustedNetworks { get; set; }

    public IReadOnlyList<System.Net.IPNetwork> ParseNetworks()
    {
        var networks = new List<System.Net.IPNetwork>();
        foreach (var raw in (TrustedNetworks ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!System.Net.IPNetwork.TryParse(raw, out var network))
                throw new InvalidOperationException($"FORWARDED_TRUSTED_NETWORKS: '{raw}' no es una red CIDR válida (ej. 10.0.0.0/8).");
            networks.Add(network);
        }
        return networks;
    }
}

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required] public string Issuer { get; set; } = "encorely-api";
    [Required] public string Audience { get; set; } = "encorely-app";
    /// <summary>Mínimo 32 bytes. Sin valor por defecto: si falta, la API no arranca.</summary>
    [Required, MinLength(32)] public string SigningKey { get; set; } = "";
    [Range(5, 60)] public int AccessTokenMinutes { get; set; } = 15;
    [Range(1, 90)] public int RefreshTokenDays { get; set; } = 30;
}

public sealed class MatchingOptions
{
    public const string Section = "Matching";

    /// <summary>Swipes mínimos para desbloquear el radar. Lo sirve la API (/me/dna); el front no lo duplica.</summary>
    [Range(0, 500)] public int RadarMinSwipes { get; set; } = 15;
    /// <summary>Afinidad mínima (0–100) para aparecer en el radar.</summary>
    [Range(0, 100)] public int RadarMinAffinity { get; set; }
}

public sealed class ChatOptions
{
    public const string Section = "Chat";

    /// <summary>
    /// Días que el servidor conserva un mensaje cifrado. 0 = sin límite (v1: el historial vive en el servidor).
    /// Cuando la app guarde el historial local y haga ack, conviene 30 (los no entregados vencen).
    /// </summary>
    [Range(0, 3650)] public int MessageRetentionDays { get; set; }
}

public sealed class RateLimitOptions
{
    public const string Section = "RateLimit";

    /// <summary>Intentos de login/registro por IP y minuto.</summary>
    [Range(1, 10_000)] public int AuthPermitPerMinute { get; set; } = 10;
    /// <summary>Intentos fallidos seguidos antes de bloquear la cuenta temporalmente.</summary>
    [Range(1, 100)] public int MaxFailedLogins { get; set; } = 5;
    [Range(1, 1440)] public int LockoutMinutes { get; set; } = 15;
    /// <summary>Escrituras por usuario y minuto en acciones que se prestan a spam (likes, mensajes, reportes…).</summary>
    [Range(1, 100_000)] public int UserWritesPerMinute { get; set; } = 120;
}

public sealed class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Inserta géneros, canciones y eventos curados si faltan (idempotente).</summary>
    public bool Catalog { get; set; }
    /// <summary>Si tiene valor, crea los usuarios demo con esta contraseña. Nunca se versiona.</summary>
    public string? DemoUserPassword { get; set; }
}

public sealed class PushOptions
{
    public const string Section = "Push";

    public bool Enabled { get; set; }
    public string? ExpoAccessToken { get; set; }
    public string ExpoEndpoint { get; set; } = "https://exp.host/--/api/v2/push/send";
}

public sealed class LegalOptions
{
    public const string Section = "Legal";

    /// <summary>Versión vigente de términos + política de tratamiento de datos (Ley 1581).</summary>
    [Required] public string ConsentVersion { get; set; } = "2026-10-13";
    public DateOnly EffectiveDate { get; set; } = new(2026, 10, 13);
    public string PublicBaseUrl { get; set; } = "http://localhost:5000";
}
