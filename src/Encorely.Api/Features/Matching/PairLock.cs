using System.Security.Cryptography;
using Encorely.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Api.Features.Matching;

/// <summary>
/// Serializa las operaciones sobre un mismo par de usuarios (like, unmatch, bloqueo) con un advisory lock
/// de transacción: sin él, dos likes cruzados concurrentes no se ven entre sí y nunca crean el match.
/// </summary>
public static class PairLock
{
    /// <summary>Llave estable del par ordenado (SHA-256 truncado; GetHashCode cambia entre procesos).</summary>
    public static long Key(Guid x, Guid y)
    {
        var (a, b) = Match.OrderPair(x, y);
        Span<byte> data = stackalloc byte[32];
        a.TryWriteBytes(data[..16], bigEndian: true, out _);
        b.TryWriteBytes(data[16..], bigEndian: true, out _);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return BitConverter.ToInt64(hash[..8]);
    }

    /// <summary>Debe llamarse dentro de una transacción; el lock se libera al terminarla.</summary>
    public static async Task AcquireAsync(AppDbContext db, Guid x, Guid y, CancellationToken ct)
    {
        var key = Key(x, y);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }
}
