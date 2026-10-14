using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Api.Features.Sync;

public sealed class SyncService(AppDbContext db, TimeProvider clock)
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(10);
    private const int MaxOpenLinksPerUser = 3;
    private static readonly HashSet<string> KdfAlgorithms = ["argon2id", "scrypt", "pbkdf2-sha256"];

    public async Task<BackupInfoDto> PutBackupAsync(Guid userId, PutBackupRequest r, CancellationToken ct)
    {
        var algorithm = r.Kdf.Algorithm.Trim().ToLowerInvariant();
        if (!KdfAlgorithms.Contains(algorithm))
            throw ApiException.BadRequest("unsupported_kdf", "Algoritmo de derivación no soportado.", "Usa argon2id, scrypt o pbkdf2-sha256.");

        var backup = await db.Set<EncryptedBackup>().FirstOrDefaultAsync(b => b.UserId == userId, ct);
        if (backup is null)
        {
            backup = new EncryptedBackup { UserId = userId, Ciphertext = r.Ciphertext, Nonce = r.Nonce, Salt = r.Salt, KdfAlgorithm = algorithm };
            db.Add(backup);
        }
        backup.Version = r.Version;
        backup.Ciphertext = r.Ciphertext;
        backup.Nonce = r.Nonce;
        backup.Salt = r.Salt;
        backup.KdfAlgorithm = algorithm;
        backup.KdfOpsLimit = r.Kdf.OpsLimit;
        backup.KdfMemLimit = r.Kdf.MemLimit;
        backup.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return new BackupInfoDto(backup.Version, backup.Ciphertext.LongLength, backup.UpdatedAt);
    }

    public async Task<BackupDto> GetBackupAsync(Guid userId, CancellationToken ct)
    {
        var b = await db.Set<EncryptedBackup>().AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct) ?? throw BackupMissing();
        return new BackupDto(b.Version, b.Ciphertext, b.Nonce, b.Salt, new KdfParams(b.KdfAlgorithm, b.KdfOpsLimit, b.KdfMemLimit), b.UpdatedAt);
    }

    public async Task<BackupInfoDto> GetBackupInfoAsync(Guid userId, CancellationToken ct)
        => await db.Set<EncryptedBackup>().Where(b => b.UserId == userId)
               .Select(b => new BackupInfoDto(b.Version, b.Ciphertext.Length, b.UpdatedAt)).FirstOrDefaultAsync(ct)
           ?? throw BackupMissing();

    public Task DeleteBackupAsync(Guid userId, CancellationToken ct)
        => db.Set<EncryptedBackup>().Where(b => b.UserId == userId).ExecuteDeleteAsync(ct);

    public async Task<LinkRequestDto> CreateLinkAsync(Guid userId, CreateLinkRequest r, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var open = await db.Set<DeviceLinkRequest>().CountAsync(l => l.UserId == userId && l.ExpiresAt > now, ct);
        if (open >= MaxOpenLinksPerUser)
            throw new ApiException(StatusCodes.Status429TooManyRequests, "too_many_link_requests", "Tienes demasiadas vinculaciones abiertas. Espera unos minutos.");

        var link = new DeviceLinkRequest { UserId = userId, NewDeviceEphemeralKey = r.EphemeralKey, CreatedAt = now, ExpiresAt = now + LinkLifetime };
        db.Add(link);
        await db.SaveChangesAsync(ct);
        return ToDto(link);
    }

    public async Task<LinkRequestDto> GetLinkAsync(Guid userId, Guid id, CancellationToken ct)
        => ToDto(await FindOpenAsync(userId, id, ct));

    public async Task FulfillLinkAsync(Guid userId, Guid id, FulfillLinkRequest r, CancellationToken ct)
    {
        var link = await FindOpenAsync(userId, id, ct);
        // Un solo uso, atómico: si dos dispositivos responden a la vez, solo uno gana.
        var updated = await db.Set<DeviceLinkRequest>()
            .Where(l => l.Id == link.Id && l.Status == LinkRequestStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, LinkRequestStatus.Ready)
                .SetProperty(l => l.SenderEphemeralKey, r.SenderEphemeralKey)
                .SetProperty(l => l.Nonce, r.Nonce)
                .SetProperty(l => l.Ciphertext, r.Ciphertext), ct);
        if (updated == 0) throw ApiException.Conflict("link_already_fulfilled", "Esa vinculación ya fue completada.");
    }

    public Task DeleteLinkAsync(Guid userId, Guid id, CancellationToken ct)
        => db.Set<DeviceLinkRequest>().Where(l => l.Id == id && l.UserId == userId).ExecuteDeleteAsync(ct);

    private async Task<DeviceLinkRequest> FindOpenAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await db.Set<DeviceLinkRequest>().AsNoTracking().FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId && l.ExpiresAt > now, ct)
               ?? throw ApiException.NotFound("link_not_found", "Esa vinculación no existe o ya venció.");
    }

    private static LinkRequestDto ToDto(DeviceLinkRequest l)
        => new(l.Id, l.Status, l.NewDeviceEphemeralKey, l.SenderEphemeralKey, l.Nonce, l.Ciphertext, l.ExpiresAt);

    private static ApiException BackupMissing() => ApiException.NotFound("backup_missing", "No tienes un respaldo guardado.");
}
