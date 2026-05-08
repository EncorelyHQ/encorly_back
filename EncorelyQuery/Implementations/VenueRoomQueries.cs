using Dapper;
using EncorelyModels;
using EncorelyQuery.Interfaces;

namespace EncorelyQuery.Implementations;

public class VenueRoomQueries : IVenueRoomQueries
{
    private readonly IDbConnectionFactory _connectionFactory;

    public VenueRoomQueries(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<VenueRoom?> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"VenueRooms\" WHERE \"Id\" = @Id";
        return await connection.QueryFirstOrDefaultAsync<VenueRoom>(sql, new { Id = id });
    }

    public async Task<IEnumerable<VenueRoom>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"VenueRooms\"";
        return await connection.QueryAsync<VenueRoom>(sql);
    }

    public async Task<IEnumerable<VenueRoom>> GetActiveRoomsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM \"VenueRooms\" WHERE \"IsActive\" = true";
        return await connection.QueryAsync<VenueRoom>(sql);
    }
}
