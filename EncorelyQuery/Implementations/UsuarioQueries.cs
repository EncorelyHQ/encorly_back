using Dapper;
using EncorelyModels;
using EncorelyQuery.Interfaces;

namespace EncorelyQuery.Implementations;

public class UsuarioQueries : IUsuarioQueries
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UsuarioQueries(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Usuario?> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Users\" WHERE \"Id\" = @Id";
        return await connection.QueryFirstOrDefaultAsync<Usuario>(sql, new { Id = id });
    }

    public async Task<Usuario?> GetByEmailAsync(string email)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Users\" WHERE \"Email\" = @Email";
        return await connection.QueryFirstOrDefaultAsync<Usuario>(sql, new { Email = email });
    }

    public async Task<Usuario?> GetBySpotifyIdAsync(string spotifyId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Users\" WHERE \"SpotifyId\" = @SpotifyId";
        return await connection.QueryFirstOrDefaultAsync<Usuario>(sql, new { SpotifyId = spotifyId });
    }

    public async Task<IEnumerable<Usuario>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Users\"";
        return await connection.QueryAsync<Usuario>(sql);
    }

    public async Task<IEnumerable<Usuario>> GetByMoodExceptAsync(ConcertMood mood, Guid exceptUserId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Users\" WHERE \"Id\" != @UserId AND \"Mood\" = @Mood";
        return await connection.QueryAsync<Usuario>(sql, new { UserId = exceptUserId, Mood = (int)mood });
    }
}
