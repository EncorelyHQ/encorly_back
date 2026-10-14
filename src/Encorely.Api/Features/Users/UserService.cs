using Encorely.Api.Features.Matching;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Encorely.Api.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Users;

public sealed class UserService(AppDbContext db, Notifier notifier, IOptions<LegalOptions> legal, TimeProvider clock)
{
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    public async Task<MeResponse> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Genres).ThenInclude(g => g.Genre).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw ApiException.Unauthorized(ErrorCodes.Unauthorized, "Necesitas iniciar sesión.");
        return ToMe(user);
    }

    public async Task<MeResponse> UpdateAsync(Guid userId, UpdateMeRequest request, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Genres).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw ApiException.Unauthorized(ErrorCodes.Unauthorized, "Necesitas iniciar sesión.");

        if (request.DisplayName is not null)
        {
            var name = request.DisplayName.Trim();
            if (name.Length < 2) throw Invalid("displayName: debe tener al menos 2 caracteres.");
            user.DisplayName = name;
        }

        if (request.City is not null)
        {
            var city = request.City.Trim();
            if (city.Length < 2) throw Invalid("city: debe tener al menos 2 caracteres.");
            user.City = city;
        }

        if (request.Bio is not null) user.Bio = Text.Clean(request.Bio);

        if (request.BirthDate is { } birth)
        {
            var today = Today();
            if (birth > today || birth.Year < 1900) throw Invalid("birthDate: la fecha no es válida.");
            if (User.AgeOn(birth, today) < 18) throw ApiException.BadRequest(ErrorCodes.UnderAge, "Debes tener 18 años o más.");
            user.BirthDate = birth;
        }

        if (request.Genres is not null) await ReplaceGenresAsync(user, request.Genres, ct);

        user.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var peers = await db.Matches
            .Where(m => m.Status == MatchStatus.Active && (m.UserAId == userId || m.UserBId == userId))
            .Select(m => new { m.Id, Peer = m.UserAId == userId ? m.UserBId : m.UserAId })
            .ToListAsync(ct);

        // Las FK en cascada borran swipes, likes, matches, mensajes, llaves, tokens, push tokens y consentimientos.
        var deleted = await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(ct);
        if (deleted == 0) throw ApiException.Unauthorized(ErrorCodes.Unauthorized, "Necesitas iniciar sesión.");
        await tx.CommitAsync(ct);

        foreach (var p in peers) await notifier.MatchClosedAsync(p.Id, p.Peer);
    }

    public async Task AcceptConsentAsync(Guid userId, ConsentRequest request, CancellationToken ct)
    {
        if (request.Version != legal.Value.ConsentVersion)
            throw ApiException.BadRequest(ErrorCodes.ConsentRequired, "Debes aceptar los términos y la política de datos vigentes.");

        db.UserConsents.Add(new UserConsent { UserId = userId, Version = request.Version, AcceptedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
    }

    public async Task<DataExportResponse> ExportAsync(Guid userId, CancellationToken ct)
    {
        var profile = await GetAsync(userId, ct);

        var swipes = await db.Swipes.AsNoTracking().Where(s => s.UserId == userId)
            .Join(db.Songs, s => s.SongId, song => song.Id, (s, song) => new { song.Title, song.Artist, s.Direction, s.CreatedAt })
            .OrderBy(x => x.CreatedAt).ToListAsync(ct);

        var events = await db.EventAttendances.AsNoTracking().Where(a => a.UserId == userId)
            .Join(db.Events, a => a.EventId, e => e.Id, (a, e) => new { e.Name, e.StartsAt })
            .OrderBy(e => e.StartsAt).ToListAsync(ct);

        var matches = await db.Matches.AsNoTracking().Where(m => m.UserAId == userId || m.UserBId == userId)
            .OrderBy(m => m.CreatedAt).ToListAsync(ct);
        var peerIds = matches.Select(m => m.PeerOf(userId)).Distinct().ToList();
        var peerNames = await db.Users.AsNoTracking().Where(u => peerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var matchIds = matches.Select(m => m.Id).ToList();
        var sent = await db.ChatMessages.AsNoTracking().Where(c => c.SenderId == userId && matchIds.Contains(c.MatchId))
            .GroupBy(c => c.MatchId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var blocks = await db.Blocks.AsNoTracking().Where(b => b.BlockerId == userId)
            .Join(db.Users, b => b.BlockedId, u => u.Id, (b, u) => new { u.DisplayName, b.CreatedAt })
            .OrderBy(b => b.CreatedAt).ToListAsync(ct);

        var consents = await db.UserConsents.AsNoTracking().Where(c => c.UserId == userId).OrderBy(c => c.AcceptedAt)
            .Select(c => new ExportConsent(c.Version, c.AcceptedAt)).ToListAsync(ct);

        var devices = await db.DevicePushTokens.CountAsync(d => d.UserId == userId, ct);

        return new DataExportResponse(
            profile,
            swipes.Select(s => new ExportSwipe(s.Title, s.Artist, s.Direction.ToString().ToLowerInvariant(), s.CreatedAt)).ToList(),
            events.Select(e => new ExportEvent(e.Name, e.StartsAt)).ToList(),
            matches.Select(m => new ExportMatch(peerNames.GetValueOrDefault(m.PeerOf(userId), "Usuario eliminado"),
                m.Status.ToString().ToLowerInvariant(), m.CreatedAt, sent.GetValueOrDefault(m.Id))).ToList(),
            blocks.Select(b => new ExportBlock(b.DisplayName, b.CreatedAt)).ToList(),
            consents,
            devices,
            clock.GetUtcNow());
    }

    private async Task ReplaceGenresAsync(User user, IReadOnlyList<string> slugs, CancellationToken ct)
    {
        var normalized = slugs.Select(s => s.Trim().ToLowerInvariant()).ToList();
        if (normalized.Distinct().Count() != normalized.Count) throw Invalid("genres: hay géneros repetidos.");

        var found = await db.Genres.AsNoTracking().Where(g => normalized.Contains(g.Slug)).Select(g => g.Id).ToListAsync(ct);
        if (found.Count != normalized.Count)
            throw ApiException.BadRequest(ErrorCodes.UnknownGenre, "Alguno de los géneros no existe.");

        // Diferencial: borrar y reinsertar la misma llave compuesta en un SaveChanges choca en el change tracker.
        foreach (var stale in user.Genres.Where(g => !found.Contains(g.GenreId)).ToList()) db.UserGenres.Remove(stale);
        var kept = user.Genres.Select(g => g.GenreId).ToHashSet();
        foreach (var id in found.Where(id => !kept.Contains(id))) user.Genres.Add(new UserGenre { UserId = user.Id, GenreId = id });
    }

    private MeResponse ToMe(User user) => new(
        user.Id, user.Email, user.DisplayName, user.BirthDate,
        user.BirthDate is { } b ? User.AgeOn(b, Today()) : null,
        user.City, user.Bio,
        user.Genres.Select(g => g.Genre!.Slug).Order().ToList(),
        user.OnboardingCompleted, user.Role, user.CreatedAt);

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(ColombiaOffset).DateTime);

    private static ApiException Invalid(string detail) => ApiException.BadRequest(ErrorCodes.ValidationFailed, "Revisa los datos enviados.", detail);
}
