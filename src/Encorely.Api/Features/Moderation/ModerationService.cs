using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Encorely.Api.Features.Matching;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Encorely.Api.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Api.Features.Moderation;

public static class ModerationErrors
{
    public const string CannotBlockSelf = "cannot_block_self";
    public const string InvalidEvidence = "invalid_evidence";
}

public sealed class ModerationService(AppDbContext db, Notifier notifier, TimeProvider clock)
{
    private const string FrankingContext = "ENCORELY-FRANK-v1";

    public async Task BlockAsync(Guid userId, BlockRequest request, CancellationToken ct)
    {
        var targetId = request.UserId;
        if (targetId == userId)
            throw ApiException.BadRequest(ModerationErrors.CannotBlockSelf, "No puedes bloquearte a ti mismo.");
        await EnsureUserExistsAsync(targetId, ct);

        var (a, b) = Match.OrderPair(userId, targetId);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await PairLock.AcquireAsync(db, userId, targetId, ct);

        if (!await db.Blocks.AnyAsync(x => x.BlockerId == userId && x.BlockedId == targetId, ct))
            db.Blocks.Add(new Block { BlockerId = userId, BlockedId = targetId, CreatedAt = clock.GetUtcNow() });

        var match = await db.Matches.FirstOrDefaultAsync(m => m.UserAId == a && m.UserBId == b && m.Status == MatchStatus.Active, ct);
        if (match is not null)
        {
            match.Status = MatchStatus.Closed;
            match.CloseReason = MatchCloseReason.Blocked;
            match.ClosedAt = clock.GetUtcNow();
            match.ClosedByUserId = userId;
        }
        await db.SaveChangesAsync(ct);
        await db.UserLikes.Where(l => (l.FromUserId == a && l.ToUserId == b) || (l.FromUserId == b && l.ToUserId == a)).ExecuteDeleteAsync(ct);
        await db.ProfileReactions.Where(r => (r.FromUserId == a && r.ToUserId == b) || (r.FromUserId == b && r.ToUserId == a)).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        if (match is not null) await notifier.MatchClosedAsync(match.Id, a, b);
    }

    public async Task<IReadOnlyList<BlockedUserDto>> ListBlocksAsync(Guid userId, CancellationToken ct)
        => await (from bl in db.Blocks.AsNoTracking()
                  join u in db.Users.AsNoTracking() on bl.BlockedId equals u.Id
                  where bl.BlockerId == userId
                  orderby bl.CreatedAt descending
                  select new BlockedUserDto(u.Id, u.DisplayName, bl.CreatedAt)).ToListAsync(ct);

    public async Task UnblockAsync(Guid userId, Guid blockedId, CancellationToken ct)
        => await db.Blocks.Where(b => b.BlockerId == userId && b.BlockedId == blockedId).ExecuteDeleteAsync(ct);

    public async Task<ReportCreatedDto> ReportAsync(Guid userId, ReportRequest request, CancellationToken ct)
    {
        var reportedId = request.UserId;
        if (reportedId == userId)
            throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "No puedes reportarte a ti mismo.");
        await EnsureUserExistsAsync(reportedId, ct);

        var (a, b) = Match.OrderPair(userId, reportedId);
        if (request.MatchId is { } matchId
            && !await db.Matches.AnyAsync(m => m.Id == matchId && m.UserAId == a && m.UserBId == b, ct))
            throw ApiException.NotFound(ErrorCodes.MatchNotFound, "No encontramos ese match.");

        var evidence = await BuildEvidenceAsync(userId, reportedId, a, b, request.Evidence ?? [], ct);

        var detail = request.Detail?.Trim();
        var report = new Report
        {
            ReporterId = userId,
            ReportedUserId = reportedId,
            MatchId = request.MatchId,
            Reason = request.Reason,
            Detail = string.IsNullOrEmpty(detail) ? null : detail,
            Evidence = evidence,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);
        return new ReportCreatedDto(report.Id);
    }

    private async Task<List<ReportEvidence>> BuildEvidenceAsync(
        Guid reporterId, Guid reportedId, Guid a, Guid b, IReadOnlyList<ReportEvidenceRequest> items, CancellationToken ct)
    {
        if (items.Count == 0) return [];

        var ids = items.Select(i => i.MessageId).Distinct().ToList();
        var messages = await (
            from m in db.ChatMessages.AsNoTracking()
            join mt in db.Matches.AsNoTracking() on m.MatchId equals mt.Id
            where ids.Contains(m.Id) && mt.UserAId == a && mt.UserBId == b && m.SenderId == reportedId
            select new { m.Id, m.MatchId, m.FrankingTag }).ToDictionaryAsync(m => m.Id, ct);

        // El ack del receptor o la retención ya pudieron borrar el mensaje: su compromiso sigue en franking_records.
        var missing = ids.Where(id => !messages.ContainsKey(id)).ToList();
        var kept = missing.Count == 0
            ? new Dictionary<Guid, Evidence>()
            : await (
                from f in db.MessageFrankingRecords.AsNoTracking()
                join mt in db.Matches.AsNoTracking() on f.MatchId equals mt.Id
                where missing.Contains(f.MessageId) && mt.UserAId == a && mt.UserBId == b
                      && f.SenderId == reportedId && f.RecipientId == reporterId
                select new Evidence(f.MessageId, f.MatchId, f.FrankingTag)).ToDictionaryAsync(f => f.Id, ct);

        var result = new List<ReportEvidence>(items.Count);
        foreach (var item in items)
        {
            Evidence msg;
            if (messages.TryGetValue(item.MessageId, out var live)) msg = new Evidence(live.Id, live.MatchId, live.FrankingTag);
            else if (!kept.TryGetValue(item.MessageId, out msg!))
                throw ApiException.BadRequest(ModerationErrors.InvalidEvidence, "La evidencia debe ser de mensajes que te envió esa persona.");

            var verified = msg.FrankingTag is { } tag
                && TagMatches(tag, item.FrankingKey, item.MessageId, item.Plaintext)
                && EnvelopeMatches(item.Plaintext, item.MessageId, msg.MatchId, reportedId, reporterId);
            result.Add(new ReportEvidence { MessageId = item.MessageId, Plaintext = item.Plaintext, Verified = verified });
        }
        return result;
    }

    private sealed record Evidence(Guid Id, Guid MatchId, byte[]? FrankingTag);

    private static bool TagMatches(byte[] storedTag, byte[] key, Guid messageId, string plaintext)
    {
        var context = Encoding.UTF8.GetBytes(FrankingContext);
        var body = Encoding.UTF8.GetBytes(plaintext);
        var data = new byte[context.Length + 16 + body.Length];
        context.CopyTo(data, 0);
        messageId.TryWriteBytes(data.AsSpan(context.Length, 16), bigEndian: true, out _);
        body.CopyTo(data, context.Length + 16);
        var expected = HMACSHA256.HashData(key, data);
        return CryptographicOperations.FixedTimeEquals(expected, storedTag);
    }

    /// <summary>Un HMAC válido no basta: el sobre interno debe nombrar este mensaje, este match y a ambas personas.</summary>
    private static bool EnvelopeMatches(string plaintext, Guid messageId, Guid matchId, Guid senderId, Guid recipientId)
    {
        try
        {
            using var doc = JsonDocument.Parse(plaintext);
            var root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && GuidProp(root, "id") == messageId
                && GuidProp(root, "m") == matchId
                && GuidProp(root, "s") == senderId
                && GuidProp(root, "r") == recipientId;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Guid? GuidProp(JsonElement root, string name)
        => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && p.TryGetGuid(out var g) ? g : null;

    private async Task EnsureUserExistsAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id, ct))
            throw ApiException.NotFound(ErrorCodes.UserNotFound, "No encontramos a esa persona.");
    }
}
