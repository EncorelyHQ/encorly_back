namespace EncorelyApplication.Interfaces;

public interface IPlaylistService
{
    Task<object> GenerateSharedPlaylistAsync(Guid userId1, Guid userId2, string accessToken1, string accessToken2, CancellationToken ct = default);
}
