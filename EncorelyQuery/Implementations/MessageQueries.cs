using Dapper;
using EncorelyModels;
using EncorelyQuery.Interfaces;

namespace EncorelyQuery.Implementations;

public class MessageQueries : IMessageQueries
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MessageQueries(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Message?> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Messages\" WHERE \"Id\" = @Id";
        return await connection.QueryFirstOrDefaultAsync<Message>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Message>> GetByMatchIdAsync(Guid matchId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Messages\" WHERE \"MatchId\" = @MatchId ORDER BY \"CreatedAt\" ASC";
        return await connection.QueryAsync<Message>(sql, new { MatchId = matchId });
    }
}
