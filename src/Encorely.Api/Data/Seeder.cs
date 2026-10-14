using System.Reflection;
using System.Text.Json;
using Encorely.Api.Features.Events;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Data;

/// <summary>
/// Seed curado e idempotente: géneros, canciones (previews de iTunes) y eventos se insertan si faltan.
/// Los usuarios demo solo se crean si hay DEMO_USER_PASSWORD; la contraseña nunca está en el repo.
/// </summary>
public sealed class Seeder(
    AppDbContext db,
    PasswordHasher hasher,
    IOptions<SeedOptions> options,
    IOptions<LegalOptions> legal,
    TimeProvider clock,
    ILogger<Seeder> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var o = options.Value;
        if (o.Catalog)
        {
            await SeedGenresAsync(ct);
            await SeedSongsAsync(ct);
            await SeedEventsAsync(ct);
        }
        if (!string.IsNullOrWhiteSpace(o.DemoUserPassword))
            await SeedDemoUsersAsync(o.DemoUserPassword, ct);
    }

    private async Task SeedGenresAsync(CancellationToken ct)
    {
        var existing = await db.Genres.Select(g => g.Slug).ToHashSetAsync(ct);
        var added = Read<SeedGenre>("genres.json").Where(g => !existing.Contains(g.Slug))
            .Select(g => new Genre { Slug = g.Slug, Name = g.Name }).ToList();
        db.Genres.AddRange(added);
        await db.SaveChangesAsync(ct);
        if (added.Count > 0) logger.LogInformation("Seed: {Count} géneros", added.Count);
    }

    private async Task SeedSongsAsync(CancellationToken ct)
    {
        var genres = await db.Genres.ToDictionaryAsync(g => g.Slug, g => g.Id, ct);
        var existing = await db.Songs.Select(s => s.Title.ToLower() + "|" + s.Artist.ToLower()).ToHashSetAsync(ct);
        var added = Read<SeedSong>("songs.json")
            .Where(s => !existing.Contains(s.Title.ToLowerInvariant() + "|" + s.Artist.ToLowerInvariant()))
            .Select(s => new Song
            {
                Title = s.Title,
                Artist = s.Artist,
                GenreId = genres.TryGetValue(s.Genre, out var id) ? id : throw new InvalidOperationException($"Género desconocido en seed: {s.Genre}"),
                CoverUrl = s.CoverUrl,
                PreviewUrl = s.PreviewUrl,
                ITunesTrackId = s.ITunesTrackId,
            }).ToList();
        db.Songs.AddRange(added);
        await db.SaveChangesAsync(ct);
        if (added.Count > 0) logger.LogInformation("Seed: {Count} canciones", added.Count);
    }

    private async Task SeedEventsAsync(CancellationToken ct)
    {
        var existing = await db.Events.Select(e => e.Name).ToHashSetAsync(ct);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(ColombiaOffset).Date);
        var added = Read<SeedEvent>("events.json").Where(e => !existing.Contains(e.Name)).Select(e => new Event
        {
            Name = e.Name,
            ArtistLineup = [.. e.ArtistLineup],
            Venue = e.Venue,
            City = e.City,
            // Fechas relativas al día del seed: la demo siempre tiene eventos futuros.
            StartsAt = new DateTimeOffset(today.AddDays(e.DaysFromNow).ToDateTime(e.LocalTime), ColombiaOffset).ToUniversalTime(),
            ImageUrl = e.ImageUrl,
            Description = e.Description,
        }).ToList();
        db.Events.AddRange(added);
        await db.SaveChangesAsync(ct);
        if (added.Count > 0) logger.LogInformation("Seed: {Count} eventos", added.Count);
    }

    private async Task SeedDemoUsersAsync(string password, CancellationToken ct)
    {
        var demo = Read<SeedUser>("demo-users.json");
        var emails = demo.Select(d => d.Email).ToList();
        var existing = await db.Users.Where(u => emails.Contains(u.Email)).Select(u => u.Email).ToHashSetAsync(ct);
        var pending = demo.Where(d => !existing.Contains(d.Email)).ToList();
        if (pending.Count == 0) return;

        var genres = await db.Genres.ToDictionaryAsync(g => g.Slug, g => g.Id, ct);
        var songs = await db.Songs.Where(s => s.IsActive).OrderBy(s => s.Title).Select(s => new { s.Id, s.GenreId }).ToListAsync(ct);
        // El admin puede crear eventos con el mismo nombre: el diccionario no debe reventar por duplicados.
        var events = (await db.Events.OrderBy(e => e.CreatedAt).Select(e => new { e.Name, e.Id }).ToListAsync(ct))
            .GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.First().Id);
        var hash = hasher.Hash(password);
        var now = clock.GetUtcNow();

        foreach (var d in pending)
        {
            var user = new User
            {
                Email = d.Email,
                PasswordHash = hash,
                DisplayName = d.DisplayName,
                BirthDate = d.BirthDate,
                City = d.City,
                Bio = d.Bio,
                Role = d.Admin ? UserRole.Admin : UserRole.User,
                Genres = d.Genres.Select(slug => new UserGenre { GenreId = genres[slug] }).ToList(),
            };
            db.Users.Add(user);
            db.UserConsents.Add(new UserConsent { UserId = user.Id, Version = legal.Value.ConsentVersion, AcceptedAt = now });

            // Swipes deterministas: like a las canciones de sus géneros con peso, dislike a una de cada tres del resto.
            var scores = new Dictionary<int, UserGenreScore>();
            var index = 0;
            foreach (var song in songs)
            {
                index++;
                var slug = genres.First(g => g.Value == song.GenreId).Key;
                Features.Music.SwipeDirection? direction = d.Likes.TryGetValue(slug, out var take) && take > 0
                    ? Features.Music.SwipeDirection.Like
                    : (index + d.Email.Length) % 3 == 0 ? Features.Music.SwipeDirection.Dislike : null;
                if (direction is null) continue;
                if (direction == Features.Music.SwipeDirection.Like) d.Likes[slug] = take - 1;

                db.Swipes.Add(new Swipe { UserId = user.Id, SongId = song.Id, Direction = direction.Value, CreatedAt = now });
                var score = scores.TryGetValue(song.GenreId, out var s) ? s : scores[song.GenreId] = new UserGenreScore { UserId = user.Id, GenreId = song.GenreId };
                if (direction == Features.Music.SwipeDirection.Like) score.Likes++; else score.Dislikes++;
            }
            db.UserGenreScores.AddRange(scores.Values);

            foreach (var name in d.Attends.Distinct())
                if (events.TryGetValue(name, out var eventId))
                    db.EventAttendances.Add(new EventAttendance { UserId = user.Id, EventId = eventId, CreatedAt = now });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: {Count} usuarios demo", pending.Count);
    }

    private static List<T> Read<T>(string file)
    {
        var name = $"Encorely.Api.Data.Seed.{file}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Recurso de seed no encontrado: {name}");
        return JsonSerializer.Deserialize<List<T>>(stream, Json) ?? [];
    }

    private sealed record SeedGenre(string Slug, string Name);
    private sealed record SeedSong(string Title, string Artist, string Genre, string CoverUrl, string? PreviewUrl, long? ITunesTrackId);
    private sealed record SeedEvent(string Name, string[] ArtistLineup, string Venue, string City, int DaysFromNow, TimeOnly LocalTime, string? ImageUrl, string? Description);
    private sealed record SeedUser(string Email, string DisplayName, DateOnly BirthDate, string City, string? Bio, string[] Genres, Dictionary<string, int> Likes, string[] Attends, bool Admin = false);
}

public static class DatabaseStartup
{
    public static async Task PrepareDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (options.MigrateOnStartup)
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Seeder>().SeedAsync();
    }
}
