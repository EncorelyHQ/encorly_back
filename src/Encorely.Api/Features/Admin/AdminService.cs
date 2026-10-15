using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Events;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Encorely.Api.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Encorely.Api.Features.Admin;

public sealed class AdminService(AppDbContext db, Notifier notifier, SessionRevoker sessions, TimeProvider clock, ILogger<AdminService> logger)
{
    public async Task<IReadOnlyList<AdminEventDto>> ListEventsAsync(bool includePast, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await db.Events
            .Where(e => includePast || e.StartsAt > now)
            .OrderBy(e => e.StartsAt)
            .Select(e => new AdminEventDto(e.Id, e.Name, e.ArtistLineup, e.Venue, e.City, e.StartsAt, e.ImageUrl, e.Description,
                e.IsPublished, db.EventAttendances.Count(a => a.EventId == e.Id), e.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<AdminEventDto> CreateEventAsync(AdminEventRequest request, CancellationToken ct)
    {
        var e = new Event { Name = "", Venue = "", City = "" };
        Apply(e, request);
        db.Events.Add(e);
        await db.SaveChangesAsync(ct);
        return await GetEventAsync(e.Id, ct);
    }

    public async Task<AdminEventDto> UpdateEventAsync(Guid id, AdminEventRequest request, CancellationToken ct)
    {
        var e = await db.Events.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw EventNotFound();
        Apply(e, request);
        await db.SaveChangesAsync(ct);
        return await GetEventAsync(id, ct);
    }

    public async Task DeleteEventAsync(Guid id, CancellationToken ct)
    {
        if (await db.Events.Where(e => e.Id == id).ExecuteDeleteAsync(ct) == 0) throw EventNotFound();
    }

    public async Task<IReadOnlyList<AdminSongDto>> ListSongsAsync(string? query, string? genre, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 200) throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "El límite debe estar entre 1 y 200.");
        var songs = db.Songs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim()}%";
            songs = songs.Where(s => EF.Functions.ILike(s.Title, pattern) || EF.Functions.ILike(s.Artist, pattern));
        }
        if (!string.IsNullOrWhiteSpace(genre)) songs = songs.Where(s => s.Genre!.Slug == genre);

        return await songs.OrderBy(s => s.Artist).ThenBy(s => s.Title).Take(limit)
            .Select(s => new AdminSongDto(s.Id, s.Title, s.Artist, s.Genre!.Slug, s.CoverUrl, s.PreviewUrl, s.ITunesTrackId, s.IsActive,
                db.Swipes.Count(w => w.SongId == s.Id && w.Direction == SwipeDirection.Like),
                db.Swipes.Count(w => w.SongId == s.Id && w.Direction == SwipeDirection.Dislike)))
            .ToListAsync(ct);
    }

    public async Task<AdminSongDto> CreateSongAsync(AdminSongRequest request, CancellationToken ct)
    {
        var song = new Song { Title = "", Artist = "", CoverUrl = "" };
        await ApplyAsync(song, request, ct);
        db.Songs.Add(song);
        await SaveSongAsync(ct);
        return await GetSongAsync(song.Id, ct);
    }

    /// <summary>No hay borrado: los swipes ya sumaron al ADN. isActive=false la saca del feed.</summary>
    public async Task<AdminSongDto> UpdateSongAsync(Guid id, AdminSongRequest request, CancellationToken ct)
    {
        var song = await db.Songs.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw ApiException.NotFound(ErrorCodes.SongNotFound, "No encontramos esa canción.");
        var genreChanged = (await GenreIdAsync(request.Genre, ct)) != song.GenreId;
        if (genreChanged && await db.Swipes.AnyAsync(w => w.SongId == id, ct))
            throw ApiException.Conflict("song_has_swipes", "No se puede cambiar el género de una canción ya calificada.",
                "Cambiarlo dejaría inconsistente el ADN de quienes la calificaron. Desactívala y crea otra.");
        await ApplyAsync(song, request, ct);
        await SaveSongAsync(ct);
        return await GetSongAsync(id, ct);
    }

    public async Task<IReadOnlyList<AdminReportDto>> ListReportsAsync(ReportStatus status, CancellationToken ct)
    {
        var reports = await db.Reports.Where(r => r.Status == status).OrderBy(r => r.CreatedAt).Take(200).ToListAsync(ct);
        return await ToDtosAsync(reports, ct);
    }

    public async Task<AdminReportDto> ResolveReportAsync(Guid adminId, Guid reportId, ResolveReportRequest request, CancellationToken ct)
    {
        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == reportId, ct)
            ?? throw ApiException.NotFound("report_not_found", "No encontramos ese reporte.");
        if (report.Status != ReportStatus.Open)
            throw ApiException.Conflict("report_already_resolved", "Ese reporte ya fue resuelto.");

        if (request.Action == ReportAction.Ban)
        {
            if (report.ReportedUserId is not { } reported)
                throw ApiException.BadRequest("reported_user_deleted", "La cuenta reportada ya no existe.");
            await SetUserStatusAsync(adminId, reported, UserStatus.Banned, ct);
        }

        report.Status = request.Action == ReportAction.Dismiss ? ReportStatus.Dismissed : ReportStatus.Resolved;
        report.ResolutionNote = Text.Clean(request.Note);
        report.ResolvedByUserId = adminId;
        report.ResolvedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([report], ct))[0];
    }

    /// <summary>Suspender: cierra todos sus matches activos, revoca sus sesiones y el JWT vigente deja de servir (UseActiveUserCheck).</summary>
    public async Task SetUserStatusAsync(Guid adminId, Guid userId, UserStatus status, CancellationToken ct)
    {
        if (userId == adminId) throw ApiException.BadRequest("cannot_change_own_status", "No puedes cambiar el estado de tu propia cuenta.");
        var now = clock.GetUtcNow();
        List<Match> closed = [];
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            // Exclusivo: un like en vuelo (que toma este mismo lock compartido) termina antes de suspender o ve la cuenta ya suspendida.
            await AdvisoryLock.AcquireAsync(db, MatchingService.AccountStateScope, userId, ct);
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw ApiException.NotFound(ErrorCodes.UserNotFound, "No encontramos esa cuenta.");
            if (user.Status == status) return;

            user.Status = status;
            user.UpdatedAt = now;
            if (status == UserStatus.Active)
            {
                // Reactivar levanta también el veto de re-registro.
                var hash = BannedEmail.HashOf(user.Email);
                await db.BannedEmails.Where(b => b.EmailHash == hash).ExecuteDeleteAsync(ct);
            }
            if (status == UserStatus.Banned)
            {
                var emailHash = BannedEmail.HashOf(user.Email);
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO banned_emails (email_hash, created_at) VALUES ({emailHash}, {now}) ON CONFLICT (email_hash) DO NOTHING", ct);
                closed = await db.Matches.Where(m => m.Status == MatchStatus.Active && (m.UserAId == userId || m.UserBId == userId)).ToListAsync(ct);
                foreach (var m in closed)
                {
                    m.Status = MatchStatus.Closed;
                    m.CloseReason = MatchCloseReason.Banned;
                    m.ClosedAt = now;
                    m.ClosedByUserId = adminId;
                }
                // Como en unmatch y bloqueo: si se reactiva, volver a hacer match exige dos likes nuevos (un like viejo de
                // quien lo reportó no debe bastar para rehacer el contacto).
                await db.UserLikes.Where(l => l.FromUserId == userId || l.ToUserId == userId).ExecuteDeleteAsync(ct);
                await db.ProfileReactions.Where(r => r.FromUserId == userId || r.ToUserId == userId).ExecuteDeleteAsync(ct);
                // Sesiones y avisos push del suspendido terminan con su acceso. El corte del hub de RevokeAllAsync puede
                // adelantarse al commit (y reconectar con la cuenta aún activa): por eso se repite el Disconnect tras el commit.
                await sessions.RevokeAllAsync(userId, ct);
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        logger.LogInformation("Cuenta {UserId} pasó a {Status} por el admin {AdminId}", userId, status, adminId);
        if (status == UserStatus.Banned) notifier.Disconnect(userId);
        foreach (var m in closed) await notifier.MatchClosedAsync(m.Id, m.UserAId, m.UserBId);
    }

    private async Task<List<AdminReportDto>> ToDtosAsync(List<Report> reports, CancellationToken ct)
    {
        var userIds = reports.SelectMany(r => new[] { r.ReporterId, r.ReportedUserId }).OfType<Guid>().Distinct().ToList();
        var users = await db.Users.Where(u => userIds.Contains(u.Id))
            .Select(u => new AdminUserRef(u.Id, u.DisplayName, u.Email, u.Status)).ToDictionaryAsync(u => u.Id, ct);
        var reportedIds = reports.Select(r => r.ReportedUserId).OfType<Guid>().Distinct().ToList();
        // Se cuentan personas, no reportes: insistir no infla el historial. Un reporte de una cuenta ya borrada cuenta solo.
        var reporters = (await db.Reports.Where(r => r.ReportedUserId != null && reportedIds.Contains(r.ReportedUserId.Value))
                .Select(r => new { Reported = r.ReportedUserId!.Value, r.ReporterId, r.Id }).ToListAsync(ct))
            .ToLookup(x => x.Reported, x => x.ReporterId?.ToString() ?? x.Id.ToString());
        int OthersReporting(Report r) => r.ReportedUserId is { } reported
            ? reporters[reported].Where(k => k != (r.ReporterId?.ToString() ?? r.Id.ToString())).Distinct().Count()
            : 0;

        return reports.Select(r => new AdminReportDto(
            r.Id,
            r.ReporterId is { } a ? users.GetValueOrDefault(a) : null,
            r.ReportedUserId is { } b ? users.GetValueOrDefault(b) : null,
            r.MatchId,
            r.Reason,
            r.Detail,
            r.Evidence.Select(e => new AdminEvidenceDto(e.MessageId, e.Plaintext, e.Verified)).ToList(),
            r.Status,
            r.ResolutionNote,
            r.CreatedAt,
            r.ResolvedAt,
            OthersReporting(r),
            r.EvidencePurgedAt)).ToList();
    }

    private static void Apply(Event e, AdminEventRequest r)
    {
        e.Name = r.Name.Trim();
        e.ArtistLineup = r.ArtistLineup.Select(a => a.Trim()).Where(a => a.Length > 0).Distinct().ToList();
        e.Venue = r.Venue.Trim();
        e.City = r.City.Trim();
        e.StartsAt = r.StartsAt.ToUniversalTime();
        e.ImageUrl = Text.Clean(r.ImageUrl);
        e.Description = Text.Clean(r.Description);
        e.IsPublished = r.IsPublished;
        if (e.ArtistLineup.Count == 0) throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "Agrega al menos un artista.");
    }

    private async Task ApplyAsync(Song s, AdminSongRequest r, CancellationToken ct)
    {
        s.Title = r.Title.Trim();
        s.Artist = r.Artist.Trim();
        s.GenreId = await GenreIdAsync(r.Genre, ct);
        s.CoverUrl = r.CoverUrl.Trim();
        s.PreviewUrl = Text.Clean(r.PreviewUrl);
        s.ITunesTrackId = r.ITunesTrackId;
        s.IsActive = r.IsActive;
    }

    private async Task<int> GenreIdAsync(string slug, CancellationToken ct)
        => await db.Genres.Where(g => g.Slug == slug).Select(g => (int?)g.Id).FirstOrDefaultAsync(ct)
           ?? throw ApiException.BadRequest(ErrorCodes.UnknownGenre, "Ese género no existe.");

    private async Task SaveSongAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw ApiException.Conflict("song_duplicate", "Esa canción de iTunes ya está en el catálogo.");
        }
    }

    private Task<AdminEventDto> GetEventAsync(Guid id, CancellationToken ct) => db.Events.Where(e => e.Id == id)
        .Select(e => new AdminEventDto(e.Id, e.Name, e.ArtistLineup, e.Venue, e.City, e.StartsAt, e.ImageUrl, e.Description,
            e.IsPublished, db.EventAttendances.Count(a => a.EventId == e.Id), e.CreatedAt))
        .FirstAsync(ct);

    private async Task<AdminSongDto> GetSongAsync(Guid id, CancellationToken ct)
        => (await ListSongsByIdAsync(id, ct)) ?? throw ApiException.NotFound(ErrorCodes.SongNotFound, "No encontramos esa canción.");

    private Task<AdminSongDto?> ListSongsByIdAsync(Guid id, CancellationToken ct) => db.Songs.Where(s => s.Id == id)
        .Select(s => new AdminSongDto(s.Id, s.Title, s.Artist, s.Genre!.Slug, s.CoverUrl, s.PreviewUrl, s.ITunesTrackId, s.IsActive,
            db.Swipes.Count(w => w.SongId == s.Id && w.Direction == SwipeDirection.Like),
            db.Swipes.Count(w => w.SongId == s.Id && w.Direction == SwipeDirection.Dislike)))
        .FirstOrDefaultAsync(ct);

    private static ApiException EventNotFound() => ApiException.NotFound(ErrorCodes.EventNotFound, "No encontramos ese evento.");
}
