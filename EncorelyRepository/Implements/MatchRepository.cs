using Dapper;
using EncorelyModels;
using EncorelyRepository.Interfaces;

namespace EncorelyRepository.Implements;

public class MatchRepository : IMatchRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MatchRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(Match match)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO ""Matches"" (""Id"", ""UserId1"", ""UserId2"", ""AffinityScore"", ""IsHighPriority"", ""CreatedAt"")
            VALUES (@Id, @UserId1, @UserId2, @AffinityScore, @IsHighPriority, @CreatedAt)
            RETURNING ""Id""";
        
        return await connection.ExecuteScalarAsync<Guid>(sql, match);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM \"Matches\" WHERE \"Id\" = @Id";
        var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
        return affectedRows > 0;
    }
}
