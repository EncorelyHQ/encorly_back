using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Api.Infrastructure.Database;

/// <summary>
/// Advisory locks de transacción de Postgres por (ámbito, id): serializan operaciones que no tienen una fila
/// natural que bloquear (reemplazar géneros, rotar llaves, importar Spotify, cambiar el estado de una cuenta).
/// Todos exigen una transacción abierta: el lock se libera solo al confirmarla o revertirla.
/// </summary>
public static class AdvisoryLock
{
    /// <summary>Primeros 8 bytes (big-endian) de SHA-256(scope ‖ id). GetHashCode no sirve: cambia entre procesos.</summary>
    public static long Key(string scope, Guid id)
    {
        var scopeBytes = Encoding.UTF8.GetBytes(scope);
        var data = new byte[scopeBytes.Length + 16];
        scopeBytes.CopyTo(data, 0);
        id.TryWriteBytes(data.AsSpan(scopeBytes.Length), bigEndian: true, out _);
        return BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(data));
    }

    /// <summary>Exclusivo: espera a que nadie más lo tenga, compartido o no.</summary>
    public static async Task AcquireAsync(AppDbContext db, string scope, Guid id, CancellationToken ct)
    {
        EnsureTransaction(db);
        var key = Key(scope, id);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }

    /// <summary>Compartido: convive con otros compartidos y solo espera a un exclusivo.</summary>
    public static async Task AcquireSharedAsync(AppDbContext db, string scope, Guid id, CancellationToken ct)
    {
        EnsureTransaction(db);
        var key = Key(scope, id);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock_shared({key})", ct);
    }

    private static void EnsureTransaction(AppDbContext db)
    {
        // Sin transacción el lock xact se soltaría al terminar la sentencia y no protegería nada.
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("AdvisoryLock requiere una transacción abierta.");
    }
}
