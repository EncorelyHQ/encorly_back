using EncorelyModels;

namespace EncorelyApplication.Interfaces;

public interface IUserService
{
    Task<Usuario> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task UpdateSettingsAsync(Guid userId, ConcertMood mood, CancellationToken ct = default);
    Task<MusicalProfile?> GetMusicalProfileAsync(Guid userId, CancellationToken ct = default);
}
