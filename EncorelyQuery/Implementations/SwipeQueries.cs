using Dapper;
using EncorelyModels;
using EncorelyQuery.Interfaces;

namespace EncorelyQuery.Implementations;

public class SwipeQueries : ISwipeQueries
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SwipeQueries(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Swipe?> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Swipes\" WHERE \"Id\" = @Id";
        return await connection.QueryFirstOrDefaultAsync<Swipe>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Swipe>> GetByUserIdAsync(Guid userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Swipes\" WHERE \"UserId\" = @UserId";
        return await connection.QueryAsync<Swipe>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<Swipe>> GetSwipesForUserAndTrackAsync(Guid userId, string trackId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"Swipes\" WHERE \"UserId\" = @UserId AND \"TrackId\" = @TrackId";
        return await connection.QueryAsync<Swipe>(sql, new { UserId = userId, TrackId = trackId });
    }
}
