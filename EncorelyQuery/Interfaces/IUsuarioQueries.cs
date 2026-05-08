using EncorelyModels;

namespace EncorelyQuery.Interfaces;

public interface IUsuarioQueries
{
    Task<Usuario?> GetByIdAsync(Guid id);
    Task<Usuario?> GetByEmailAsync(string email);
    Task<Usuario?> GetBySpotifyIdAsync(string spotifyId);
    Task<IEnumerable<Usuario>> GetAllAsync();
    Task<IEnumerable<Usuario>> GetByMoodExceptAsync(ConcertMood mood, Guid exceptUserId);
}
