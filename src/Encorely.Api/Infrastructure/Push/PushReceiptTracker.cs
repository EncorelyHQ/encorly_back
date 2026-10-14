namespace Encorely.Api.Infrastructure.Push;

public sealed record PendingReceipt(string TicketId, string Token, DateTimeOffset SentAt);

/// <summary>
/// Tickets de Expo aceptados (status ok) a la espera de su receipt. En memoria y acotado: si el proceso se reinicia
/// se pierden, y lo único que se deja de limpiar son tokens muertos, que el siguiente ticket de error igual delata.
/// </summary>
public sealed class PushReceiptTracker
{
    public const int MaxPending = 10_000;
    /// <summary>Expo conserva los receipts 24 h; pasado ese plazo preguntar por ellos es inútil.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    private readonly Lock _gate = new();
    private readonly Queue<PendingReceipt> _pending = new();

    public int Count { get { lock (_gate) return _pending.Count; } }

    public void Add(string ticketId, string token, DateTimeOffset sentAt)
    {
        lock (_gate)
        {
            _pending.Enqueue(new PendingReceipt(ticketId, token, sentAt));
            while (_pending.Count > MaxPending) _pending.Dequeue();
        }
    }

    public IReadOnlyList<PendingReceipt> Snapshot(DateTimeOffset now)
    {
        lock (_gate)
        {
            while (_pending.TryPeek(out var oldest) && now - oldest.SentAt > MaxAge) _pending.Dequeue();
            return [.. _pending];
        }
    }

    /// <summary>Quita los tickets ya resueltos (Expo devolvió su receipt).</summary>
    public void Remove(IReadOnlySet<string> ticketIds)
    {
        lock (_gate)
        {
            var keep = _pending.Where(p => !ticketIds.Contains(p.TicketId)).ToList();
            _pending.Clear();
            foreach (var p in keep) _pending.Enqueue(p);
        }
    }
}
