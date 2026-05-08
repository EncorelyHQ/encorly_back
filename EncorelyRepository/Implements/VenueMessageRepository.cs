using Dapper;
using EncorelyModels;
using EncorelyRepository.Interfaces;

namespace EncorelyRepository.Implements;

public class VenueMessageRepository : IVenueMessageRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public VenueMessageRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(VenueMessage message)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO ""VenueMessages"" (""Id"", ""RoomId"", ""SenderId"", ""Content"", ""IsModerated"", ""Timestamp"")
            VALUES (@Id, @RoomId, @SenderId, @Content, @IsModerated, @Timestamp)
            RETURNING ""Id""";
        
        return await connection.ExecuteScalarAsync<Guid>(sql, message);
    }

    public async Task<bool> UpdateAsync(VenueMessage message)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE ""VenueMessages""
            SET ""RoomId"" = @RoomId, ""SenderId"" = @SenderId, ""Content"" = @Content, ""IsModerated"" = @IsModerated, ""Timestamp"" = @Timestamp
            WHERE ""Id"" = @Id";
        
        var affectedRows = await connection.ExecuteAsync(sql, message);
        return affectedRows > 0;
    }
}
