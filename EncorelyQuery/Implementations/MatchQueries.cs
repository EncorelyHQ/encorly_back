using Dapper;
using EncorelyModels;
using EncorelyQuery.Interfaces;

namespace EncorelyQuery.Implementations;

public class MatchQueries : IMatchQueries
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MatchQueries(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Match?> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Matches\" WHERE \"Id\" = @Id";
        return await connection.QueryFirstOrDefaultAsync<Match>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Match>> GetByUserIdAsync(Guid userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Matches\" WHERE \"UserId1\" = @UserId OR \"UserId2\" = @UserId";
        return await connection.QueryAsync<Match>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<Match>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Matches\"";
        return await connection.QueryAsync<Match>(sql);
    }
}
