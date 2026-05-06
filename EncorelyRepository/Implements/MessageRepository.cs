using Dapper;
using EncorelyModels;
using EncorelyRepository.Interfaces;

namespace EncorelyRepository.Implements;

public class MessageRepository : IMessageRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MessageRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(Message message)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO ""Messages"" (""Id"", ""MatchId"", ""SenderId"", ""Content"", ""CreatedAt"")
            VALUES (@Id, @MatchId, @SenderId, @Content, @CreatedAt)
            RETURNING ""Id""";
        
        return await connection.ExecuteScalarAsync<Guid>(sql, message);
    }
}
