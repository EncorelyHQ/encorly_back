using EncorelyModels;

namespace EncorelyRepository.Interfaces;

public interface IMessageRepository
{
    Task<Guid> CreateAsync(Message message);
}
