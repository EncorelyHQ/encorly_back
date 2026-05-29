using Dapper;
using EncorelyModels;
using EncorelyQuery.Interfaces;

namespace EncorelyQuery.Implementations;

public class VenueMessageQueries : IVenueMessageQueries
{
    private readonly IDbConnectionFactory _connectionFactory;

    public VenueMessageQueries(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<VenueMessage?> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        // Columna real "CreatedAt" -> propiedad Timestamp del modelo.
        const string sql = "SELECT \"Id\", \"RoomId\", \"SenderId\", \"Content\", \"IsModerated\", \"CreatedAt\" AS \"Timestamp\" FROM \"VenueMessages\" WHERE \"Id\" = @Id";
        return await connection.QueryFirstOrDefaultAsync<VenueMessage>(sql, new { Id = id });
    }

    public async Task<IEnumerable<VenueMessage>> GetByRoomIdAsync(Guid roomId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT \"Id\", \"RoomId\", \"SenderId\", \"Content\", \"IsModerated\", \"CreatedAt\" AS \"Timestamp\" FROM \"VenueMessages\" WHERE \"RoomId\" = @RoomId ORDER BY \"CreatedAt\" ASC";
        return await connection.QueryAsync<VenueMessage>(sql, new { RoomId = roomId });
    }
}
