using Dapper;
using EncorelyModels;
using EncorelyQuery.Interfaces;

namespace EncorelyQuery.Implementations;

public class MusicalProfileQueries : IMusicalProfileQueries
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MusicalProfileQueries(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<MusicalProfile?> GetByUserIdAsync(Guid userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"MusicalProfiles\" WHERE \"UserId\" = @UserId";
        return await connection.QueryFirstOrDefaultAsync<MusicalProfile>(sql, new { UserId = userId });
    }
}
