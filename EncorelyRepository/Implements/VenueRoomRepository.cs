using Dapper;
using EncorelyModels;
using EncorelyRepository.Interfaces;

namespace EncorelyRepository.Implements;

public class VenueRoomRepository : IVenueRoomRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public VenueRoomRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(VenueRoom room)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO ""VenueRooms"" (""Id"", ""Name"", ""Description"", ""Capacity"", ""IsActive"", ""CreatedAt"")
            VALUES (@Id, @Name, @Description, @Capacity, @IsActive, @CreatedAt)
            RETURNING ""Id""";
        
        return await connection.ExecuteScalarAsync<Guid>(sql, room);
    }

    public async Task<bool> UpdateAsync(VenueRoom room)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE ""VenueRooms""
            SET ""Name"" = @Name, ""Description"" = @Description, ""Capacity"" = @Capacity, ""IsActive"" = @IsActive
            WHERE ""Id"" = @Id";
        
        var affectedRows = await connection.ExecuteAsync(sql, room);
        return affectedRows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM \"VenueRooms\" WHERE \"Id\" = @Id";
        var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
        return affectedRows > 0;
    }
}
