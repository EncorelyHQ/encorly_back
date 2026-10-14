using Encorely.Api.Features.Matching;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Encorely.Api.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Encorely.Api.Features.Chat;

/// <summary>El servidor NUNCA descifra ni registra en logs contenido de mensajes: solo ids, seq y tamaños.</summary>
public sealed class ChatService(AppDbContext db, Notifier notifier, TimeProvider clock)
{
    public async Task<(MessageAcceptedDto Accepted, bool Created)> SendAsync(Guid userId, Guid matchId, SendMessageRequest request, CancellationToken ct)
    {
        var match = await FindMineAsync(userId, matchId, ct);
        if (match.Status != MatchStatus.Active)
            throw ApiException.Forbidden(ErrorCodes.MatchClosed, "Este chat ya está cerrado.");

        var peerId = match.PeerOf(userId);
        var blocked = await db.Blocks.AnyAsync(b =>
            (b.BlockerId == userId && b.BlockedId == peerId) || (b.BlockerId == peerId && b.BlockedId == userId), ct);
        if (blocked) throw ApiException.Forbidden(ErrorCodes.Blocked, "No puedes escribir a esta persona.");

        if (await ResolveExistingAsync(userId, matchId, request, ct) is { } same) return (same, false);
        await RejectDeliveredIdAsync(userId, matchId, request, ct);

        var senderKnown = await db.UserBoxKeys.AnyAsync(k => k.UserId == userId && k.BoxKeyId == request.SenderBoxKeyId, ct);
        if (!senderKnown)
            throw ApiException.Conflict(ErrorCodes.UnknownSenderKey, "Tu llave de envío no está publicada. Publica tus llaves y reenvía.");

        var peerCurrent = await db.UserKeys.AsNoTracking().Where(k => k.UserId == peerId).Select(k => (int?)k.CurrentBoxKeyId).FirstOrDefaultAsync(ct);
        if (peerCurrent is null)
            throw ApiException.Conflict(ErrorCodes.PeerKeysMissing, "La otra persona aún no publica sus llaves.");
        if (peerCurrent != request.RecipientBoxKeyId)
            throw ApiException.Conflict(ErrorCodes.StaleRecipientKey, "La llave del otro cambió. Actualízala y reenvía.");

        var now = clock.GetUtcNow();
        long seq;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // El UPDATE toma el lock de la fila del match: serializa los envíos y asigna seq sin huecos ni repetidos.
            var seqs = await db.Database.SqlQuery<long>(
                $"""UPDATE matches SET last_seq = last_seq + 1, last_message_at = {now} WHERE id = {matchId} AND status = 'Active' RETURNING last_seq AS "Value" """)
                .ToListAsync(ct);
            if (seqs.Count == 0) throw ApiException.Forbidden(ErrorCodes.MatchClosed, "Este chat ya está cerrado.");
            seq = seqs[0];

            db.ChatMessages.Add(new ChatMessage
            {
                Id = request.Id,
                MatchId = matchId,
                SenderId = userId,
                Seq = seq,
                Version = (short)request.Version,
                SenderBoxKeyId = request.SenderBoxKeyId,
                RecipientBoxKeyId = request.RecipientBoxKeyId,
                Nonce = request.Nonce,
                Ciphertext = request.Ciphertext,
                FrankingTag = request.FrankingTag,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(ct);
            if (request.FrankingTag is { } tag)
            {
                // Misma transacción que el mensaje: el ack y la retención lo borran, el compromiso queda para verificar reportes.
                var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO message_franking_records (message_id, match_id, sender_id, recipient_id, franking_tag, created_at)
                    VALUES ({request.Id}, {matchId}, {userId}, {peerId}, {tag}, {now})
                    ON CONFLICT (message_id) DO NOTHING
                    """, ct);
                // Carrera con un ack entre RejectDeliveredIdAsync y aquí: sin la fila, la evidencia verificaría contra el tag viejo.
                if (inserted == 0) throw IdConflict();
            }
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Carrera por el mismo Id: la transacción se revirtió (seq incluido); se resuelve como reintento o conflicto.
            db.ChangeTracker.Clear();
            return (await ResolveExistingAsync(userId, matchId, request, ct) ?? throw IdConflict(), false);
        }

        await notifier.MessageAvailableAsync(peerId, new MessageAvailableEvent(matchId, request.Id, seq));
        return (new MessageAcceptedDto(request.Id, seq, now), true);
    }

    public async Task<MessagePage> ListAsync(Guid userId, Guid matchId, long? before, long? after, int limit, CancellationToken ct)
    {
        await FindMineAsync(userId, matchId, ct);
        if (before is not null && after is not null)
            throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "Usa before o after, no ambos.");
        if (limit is < 1 or > 100)
            throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "limit debe estar entre 1 y 100.");

        var query = db.ChatMessages.AsNoTracking().Where(m => m.MatchId == matchId);
        query = after is not null
            ? query.Where(m => m.Seq > after.Value).OrderBy(m => m.Seq)
            : (before is not null ? query.Where(m => m.Seq < before.Value) : query).OrderByDescending(m => m.Seq);

        var rows = await query.Take(limit + 1).ToListAsync(ct);
        var hasMore = rows.Count > limit;
        var items = rows.Take(limit).Select(ToDto).ToList();
        return new MessagePage(items, hasMore ? items[^1].Seq : null);
    }

    public async Task AckAsync(Guid userId, Guid matchId, AckRequest request, CancellationToken ct)
    {
        await FindMineAsync(userId, matchId, ct);
        await db.ChatMessages
            .Where(m => m.MatchId == matchId && m.SenderId != userId && m.Seq <= request.UpToSeq)
            .ExecuteDeleteAsync(ct);
    }

    private async Task<Match> FindMineAsync(Guid userId, Guid matchId, CancellationToken ct)
        => await db.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.Id == matchId && (m.UserAId == userId || m.UserBId == userId), ct)
            ?? throw ApiException.NotFound(ErrorCodes.MatchNotFound, "Match no encontrado.");

    /// <summary>null si el Id está libre; el aceptado si es un reintento idéntico; 409 si el Id ya es de otro contenido.</summary>
    private async Task<MessageAcceptedDto?> ResolveExistingAsync(Guid userId, Guid matchId, SendMessageRequest r, CancellationToken ct)
    {
        var e = await db.ChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == r.Id, ct);
        if (e is null) return null;
        var identical = e.MatchId == matchId && e.SenderId == userId
            && e.Nonce.AsSpan().SequenceEqual(r.Nonce)
            && e.Ciphertext.AsSpan().SequenceEqual(r.Ciphertext)
            && (e.FrankingTag is null ? r.FrankingTag is null : r.FrankingTag is not null && e.FrankingTag.AsSpan().SequenceEqual(r.FrankingTag));
        return identical ? new MessageAcceptedDto(e.Id, e.Seq, e.CreatedAt) : throw IdConflict();
    }

    /// <summary>
    /// El ack borra el mensaje pero conserva su compromiso de franking: reusar ese id crearía un duplicado (seq nuevo) cuya
    /// evidencia se verificaría contra el tag del primero, y el contenido nuevo no se podría probar al reportarlo.
    /// </summary>
    private async Task RejectDeliveredIdAsync(Guid userId, Guid matchId, SendMessageRequest r, CancellationToken ct)
    {
        var delivered = await db.MessageFrankingRecords.AsNoTracking().Where(f => f.MessageId == r.Id)
            .Select(f => new { f.MatchId, f.SenderId, f.FrankingTag }).FirstOrDefaultAsync(ct);
        if (delivered is null) return;
        var retry = delivered.MatchId == matchId && delivered.SenderId == userId
            && r.FrankingTag is not null && delivered.FrankingTag.AsSpan().SequenceEqual(r.FrankingTag);
        throw retry
            ? ApiException.Conflict(ErrorCodes.MessageAlreadyDelivered, "Ese mensaje ya se entregó y el receptor lo confirmó.")
            : IdConflict();
    }

    private static ApiException IdConflict()
        => ApiException.Conflict(ErrorCodes.MessageIdConflict, "Ese id de mensaje ya existe con otro contenido.");

    private static MessageDto ToDto(ChatMessage m)
        => new(m.Id, m.Seq, m.SenderId, m.Version, m.SenderBoxKeyId, m.RecipientBoxKeyId, m.Nonce, m.Ciphertext, m.FrankingTag, m.CreatedAt);
}
