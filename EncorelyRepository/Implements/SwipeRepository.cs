using Dapper;
using EncorelyModels;
using EncorelyRepository.Interfaces;

namespace EncorelyRepository.Implements;

public class SwipeRepository : ISwipeRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SwipeRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(Swipe swipe)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO ""Swipes"" (""Id"", ""UserId"", ""TrackId"", ""Direction"", ""CreatedAt"")
            VALUES (@Id, @UserId, @TrackId, @Direction, @CreatedAt)
            RETURNING ""Id""";
        
        return await connection.ExecuteScalarAsync<Guid>(sql, swipe);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM \"Swipes\" WHERE \"Id\" = @Id";
        var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
        return affectedRows > 0;
    }
}
