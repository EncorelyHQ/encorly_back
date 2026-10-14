using System.Security.Cryptography;
using System.Text;

namespace Encorely.Api.Features.Auth;

/// <summary>
/// Correo de una cuenta suspendida. Borrar la cuenta sigue permitido (Ley 1581), pero sin esto bastaría borrarla
/// y registrarse otra vez para evadir el ban. Se guarda solo el hash del correo normalizado.
/// </summary>
public sealed class BannedEmail
{
    public required byte[] EmailHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>SHA-256 del correo ya normalizado (trim + minúsculas).</summary>
    public static byte[] HashOf(string normalizedEmail) => SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail));
}
