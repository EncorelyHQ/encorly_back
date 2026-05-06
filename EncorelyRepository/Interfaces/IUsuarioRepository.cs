using EncorelyModels;

namespace EncorelyRepository.Interfaces;

public interface IUsuarioRepository
{
    Task<Guid> CreateAsync(Usuario usuario);
    Task<bool> UpdateAsync(Usuario usuario);
    Task<bool> DeleteAsync(Guid id);
}
