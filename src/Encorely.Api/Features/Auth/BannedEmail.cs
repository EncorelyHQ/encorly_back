using System.Security.Cryptography;
using System.Text;
using Encorely.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Auth;

/// <summary>
/// Correo de una cuenta suspendida. Borrar la cuenta sigue permitido (Ley 1581), pero sin esto bastaría borrarla
/// y registrarse otra vez para evadir el ban. Se guarda solo el HMAC del correo normalizado (ver <see cref="BannedEmailHasher"/>).
/// </summary>
public sealed class BannedEmail
{
    public required byte[] EmailHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// HMAC-SHA256 con una llave del servidor: con SHA-256 a secas, quien lea la tabla confirma por diccionario si un correo
/// está vetado. Los vetos anteriores a la llave quedaron en SHA-256 y no se pueden convertir sin el correo, así que se
/// siguen buscando (y borrando al reactivar) por los dos hashes.
/// </summary>
public sealed class BannedEmailHasher(IOptions<ModerationOptions> options)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.BannedEmailKey);

    /// <param name="normalizedEmail">Correo ya normalizado (trim + minúsculas).</param>
    public byte[] Hash(string normalizedEmail) => HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(normalizedEmail));

    public static byte[] LegacyHash(string normalizedEmail) => SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail));
}
