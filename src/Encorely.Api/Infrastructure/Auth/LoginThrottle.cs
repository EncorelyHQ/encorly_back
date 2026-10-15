using System.Security.Cryptography;
using System.Text;
using Encorely.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Infrastructure.Auth;

/// <summary>
/// Bloqueo por intentos fallidos de contraseña, por par (correo, IP). Por cuenta era un oráculo (el 429 solo existía para
/// correos reales) y una palanca de DoS: cualquiera que supiera el correo dejaba a la víctima sin login ni borrado web.
/// Así un tercero solo se bloquea a sí mismo, y un correo inexistente se bloquea igual que uno real.
/// El ataque distribuido contra una cuenta (muchas IP) lo frenan bcrypt y el mínimo de 12 caracteres, no este bloqueo.
/// En memoria: con una sola instancia basta, y un reinicio solo perdona intentos.
/// </summary>
public sealed class LoginThrottle(IHttpContextAccessor http, IOptions<RateLimitOptions> limits, TimeProvider clock) : IDisposable
{
    // Acotada: inundarla exige cientos de IP por el rate limit de /auth; lo que no cabe simplemente no se cuenta.
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 100_000 });
    private readonly Lock _create = new();

    private sealed class State
    {
        public int Failures;
        public DateTimeOffset? LockedUntil;
    }

    public bool IsLocked(string normalizedEmail)
    {
        if (!_cache.TryGetValue(Key(normalizedEmail), out State? state)) return false;
        lock (state!) return state.LockedUntil > clock.GetUtcNow();
    }

    public void RegisterFailure(string normalizedEmail)
    {
        var o = limits.Value;
        State state;
        lock (_create)
            state = _cache.GetOrCreate(Key(normalizedEmail), e =>
            {
                e.SlidingExpiration = TimeSpan.FromMinutes(o.LockoutMinutes);
                e.Size = 1;
                return new State();
            })!;

        lock (state)
        {
            if (++state.Failures < o.MaxFailedLogins) return;
            state.Failures = 0;
            state.LockedUntil = clock.GetUtcNow().AddMinutes(o.LockoutMinutes);
        }
    }

    public void Reset(string normalizedEmail) => _cache.Remove(Key(normalizedEmail));

    private string Key(string normalizedEmail)
    {
        var ip = http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        return $"{ip}|{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)))}";
    }

    public void Dispose() => _cache.Dispose();
}
