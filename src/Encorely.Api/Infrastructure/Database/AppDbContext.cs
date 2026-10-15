using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Chat;
using Encorely.Api.Features.Devices;
using Encorely.Api.Features.Events;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Profiles;
using Encorely.Api.Features.Sync;
using Encorely.Api.Features.Users;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Api.Infrastructure.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserGenre> UserGenres => Set<UserGenre>();
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<Swipe> Swipes => Set<Swipe>();
    public DbSet<UserGenreScore> UserGenreScores => Set<UserGenreScore>();
    public DbSet<UserArtist> UserArtists => Set<UserArtist>();
    public DbSet<ProfileReaction> ProfileReactions => Set<ProfileReaction>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventAttendance> EventAttendances => Set<EventAttendance>();
    public DbSet<UserLike> UserLikes => Set<UserLike>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<UserKey> UserKeys => Set<UserKey>();
    public DbSet<UserBoxKey> UserBoxKeys => Set<UserBoxKey>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<DevicePushToken> DevicePushTokens => Set<DevicePushToken>();
    public DbSet<BannedEmail> BannedEmails => Set<BannedEmail>();
    public DbSet<MessageFrankingRecord> MessageFrankingRecords => Set<MessageFrankingRecord>();
    public DbSet<DeliveredMessageId> DeliveredMessageIds => Set<DeliveredMessageId>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums como texto: legibles en la BD y sin CHECKs numéricos que den 500.
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(254);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(100);
            e.Property(x => x.SpotifyId).HasMaxLength(128);
            e.HasIndex(x => x.SpotifyId).IsUnique();
            e.Property(x => x.ShowProfileToOthers).HasDefaultValue(true);
            e.Property(x => x.DisplayName).HasMaxLength(50);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.Bio).HasMaxLength(300);
            e.Ignore(x => x.OnboardingCompleted);
            e.Ignore(x => x.HasPassword);
            e.HasMany(x => x.Genres).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserGenre>(e =>
        {
            e.HasKey(x => new { x.UserId, x.GenreId });
            e.HasOne(x => x.Genre).WithMany().HasForeignKey(x => x.GenreId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<UserConsent>(e =>
        {
            e.Property(x => x.Version).HasMaxLength(32);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.FamilyId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Genre>(e =>
        {
            e.Property(x => x.Slug).HasMaxLength(40);
            e.Property(x => x.Name).HasMaxLength(60);
            e.HasIndex(x => x.Slug).IsUnique();
        });

        b.Entity<Song>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Artist).HasMaxLength(200);
            e.Property(x => x.CoverUrl).HasMaxLength(500);
            e.Property(x => x.PreviewUrl).HasMaxLength(500);
            e.Property(x => x.ITunesTrackId).HasColumnName("itunes_track_id");
            e.HasIndex(x => x.ITunesTrackId).IsUnique();
            e.HasOne(x => x.Genre).WithMany().HasForeignKey(x => x.GenreId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Swipe>(e =>
        {
            e.HasKey(x => new { x.UserId, x.SongId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Song>().WithMany().HasForeignKey(x => x.SongId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserGenreScore>(e =>
        {
            e.HasKey(x => new { x.UserId, x.GenreId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Genre>().WithMany().HasForeignKey(x => x.GenreId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<UserArtist>(e =>
        {
            e.HasKey(x => new { x.UserId, x.Key });
            e.Property(x => x.Key).HasMaxLength(ArtistKey.MaxLength);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProfileReaction>(e =>
        {
            e.HasKey(x => new { x.FromUserId, x.ToUserId, x.TargetKind, x.TargetKey });
            e.Property(x => x.TargetKey).HasMaxLength(100);
            e.Property(x => x.TargetName).HasMaxLength(200);
            e.HasIndex(x => new { x.ToUserId, x.UpdatedAt });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.FromUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ToUserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Event>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Venue).HasMaxLength(200);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasIndex(x => new { x.City, x.StartsAt });
        });

        b.Entity<EventAttendance>(e =>
        {
            e.HasKey(x => new { x.EventId, x.UserId });
            e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserLike>(e =>
        {
            e.HasKey(x => new { x.FromUserId, x.ToUserId });
            e.HasIndex(x => x.ToUserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.FromUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ToUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.SetNull);
            e.ToTable(t => t.HasCheckConstraint("ck_user_likes_not_self", "from_user_id <> to_user_id"));
        });

        b.Entity<Match>(e =>
        {
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserAId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserBId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.SetNull);
            // Un solo match activo por par; los cerrados quedan como historial.
            e.HasIndex(x => new { x.UserAId, x.UserBId }).IsUnique().HasFilter("status = 'Active'");
            e.HasIndex(x => x.UserBId);
            e.ToTable(t => t.HasCheckConstraint("ck_matches_ordered_pair", "user_a_id < user_b_id"));
        });

        b.Entity<UserKey>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasOne<User>().WithOne().HasForeignKey<UserKey>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserBoxKey>(e =>
        {
            e.HasKey(x => new { x.UserId, x.BoxKeyId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChatMessage>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.MatchId, x.Seq }).IsUnique();
            e.HasIndex(x => x.SenderId);
            e.HasOne<Match>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Block>(e =>
        {
            e.HasKey(x => new { x.BlockerId, x.BlockedId });
            e.HasIndex(x => x.BlockedId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.BlockerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.BlockedId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("ck_blocks_not_self", "blocker_id <> blocked_id"));
        });

        b.Entity<Report>(e =>
        {
            e.Property(x => x.Detail).HasMaxLength(1000);
            e.Property(x => x.ResolutionNote).HasMaxLength(1000);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.ResolvedAt).HasFilter("evidence_purged_at IS NULL");
            // Respaldo del advisory lock de ModerationService.ReportAsync: un solo reporte abierto por par.
            e.HasIndex(x => new { x.ReporterId, x.ReportedUserId }).IsUnique().HasFilter("status = 'Open'");
            // Explícito: el parcial de arriba no cubre los reportes cerrados y EF quitaría el índice de la FK (SET NULL al borrar la cuenta).
            e.HasIndex(x => x.ReporterId);
            // El reporte sobrevive al borrado de cuentas: es evidencia de moderación.
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReportedUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Match>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.SetNull);
            e.OwnsMany(x => x.Evidence, ev => ev.ToJson());
        });

        b.Entity<EncryptedBackup>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.KdfAlgorithm).HasMaxLength(20);
            e.HasOne<User>().WithOne().HasForeignKey<EncryptedBackup>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DeviceLinkRequest>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.ExpiresAt });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DevicePushToken>(e =>
        {
            e.Property(x => x.Token).HasMaxLength(200);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.SessionId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BannedEmail>(e =>
        {
            e.HasKey(x => x.EmailHash);
        });

        b.Entity<MessageFrankingRecord>(e =>
        {
            e.HasKey(x => x.MessageId);
            e.Property(x => x.MessageId).ValueGeneratedNever();
            e.HasIndex(x => x.MatchId);
            e.HasIndex(x => x.CreatedAt);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DeliveredMessageId>(e =>
        {
            e.HasKey(x => x.MessageId);
            e.Property(x => x.MessageId).ValueGeneratedNever();
            e.HasIndex(x => x.DeliveredAt);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
