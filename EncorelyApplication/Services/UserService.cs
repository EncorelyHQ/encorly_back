using EncorelyApplication.Interfaces;
using EncorelyModels;
using EncorelyQuery.Interfaces;
using EncorelyRepository.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace EncorelyApplication.Services;

public class UserService : IUserService
{
    private readonly IUsuarioQueries _usuarioQueries;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IMusicalProfileQueries _profileQueries;
    private readonly IDistributedCache _cache;

    public UserService(
        IUsuarioQueries usuarioQueries, 
        IUsuarioRepository usuarioRepository,
        IMusicalProfileQueries profileQueries,
        IDistributedCache cache)
    {
        _usuarioQueries = usuarioQueries;
        _usuarioRepository = usuarioRepository;
        _profileQueries = profileQueries;
        _cache = cache;
    }

    public async Task<Usuario> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _usuarioQueries.GetByIdAsync(userId);
        if (user == null) throw new KeyNotFoundException("Usuario not found");
        return user;
    }

    public async Task UpdateSettingsAsync(Guid userId, ConcertMood mood, CancellationToken ct = default)
    {
        var user = await _usuarioQueries.GetByIdAsync(userId);
        if (user == null) throw new KeyNotFoundException("Usuario not found");

        user.Mood = mood;
        await _usuarioRepository.UpdateAsync(user);
    }

    public async Task<MusicalProfile?> GetMusicalProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"profile_{userId}";
        var cachedProfile = await _cache.GetStringAsync(cacheKey, ct);
        
        if (!string.IsNullOrEmpty(cachedProfile))
        {
            return JsonSerializer.Deserialize<MusicalProfile>(cachedProfile);
        }

        var profile = await _profileQueries.GetByUserIdAsync(userId);
        if (profile != null)
        {
            var options = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(1));
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(profile), options, ct);
        }

        return profile;
    }
}
