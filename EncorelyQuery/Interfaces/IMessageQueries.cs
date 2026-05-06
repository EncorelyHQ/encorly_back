using EncorelyModels;

namespace EncorelyQuery.Interfaces;

public interface IMessageQueries
{
    Task<Message?> GetByIdAsync(Guid id);
    Task<IEnumerable<Message>> GetByMatchIdAsync(Guid matchId);
}
