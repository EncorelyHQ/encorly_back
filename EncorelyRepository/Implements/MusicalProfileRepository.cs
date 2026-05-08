using Dapper;
using EncorelyModels;
using EncorelyRepository.Interfaces;

namespace EncorelyRepository.Implements;

public class MusicalProfileRepository : IMusicalProfileRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MusicalProfileRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateOrUpdateAsync(MusicalProfile profile)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO ""MusicalProfiles"" (""Id"", ""UserId"", ""Energy"", ""Danceability"", ""Valence"", ""Tempo"")
            VALUES (@Id, @UserId, @Energy, @Danceability, @Valence, @Tempo)
            ON CONFLICT (""UserId"") DO UPDATE 
            SET ""Energy"" = @Energy, ""Danceability"" = @Danceability, ""Valence"" = @Valence, ""Tempo"" = @Tempo
            RETURNING ""Id""";
        
        return await connection.ExecuteScalarAsync<Guid>(sql, profile);
    }
}
