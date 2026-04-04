using EncorelyApplication.Interfaces;
using EncorelyDomain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace EncorelyApplication.Services;

public class UserService : IUserService
{
    private readonly IEncorelyDbContext _dbContext;
    private readonly IDistributedCache _cache;

    public UserService(IEncorelyDbContext dbContext, IDistributedCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<User> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) throw new KeyNotFoundException("User not found");
        return user;
    }

    public async Task UpdateSettingsAsync(Guid userId, ConcertMood mood, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, ct);
        if (user == null) throw new KeyNotFoundException("User not found");

        user.Mood = mood;
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<MusicalProfile?> GetMusicalProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"profile_{userId}";
        var cachedProfile = await _cache.GetStringAsync(cacheKey, ct);
        
        if (!string.IsNullOrEmpty(cachedProfile))
        {
            return JsonSerializer.Deserialize<MusicalProfile>(cachedProfile);
        }

        var profile = await _dbContext.MusicalProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile != null)
        {
            var options = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(1));
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(profile), options, ct);
        }

        return profile;
    }
}
