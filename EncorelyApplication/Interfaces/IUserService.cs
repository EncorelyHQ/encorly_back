using EncorelyDomain.Entities;

namespace EncorelyApplication.Interfaces;

public interface IUserService
{
    Task<User> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task UpdateSettingsAsync(Guid userId, ConcertMood mood, CancellationToken ct = default);
}
