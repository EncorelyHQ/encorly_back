using EncorelyModels;

namespace EncorelyRepository.Interfaces;

public interface IUsuarioRepository
{
    Task<Guid> CreateAsync(Usuario usuario);
    Task<bool> UpdateAsync(Usuario usuario);
    Task<bool> DeleteAsync(Guid id);
    /// <summary>Incremento atómico (sin race) del SwipeCount. Devuelve el nuevo valor.</summary>
    Task<int> IncrementSwipeCountAsync(Guid userId);
}
