using Dapper;
using EncorelyModels;
using EncorelyRepository.Interfaces;

namespace EncorelyRepository.Implements;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UsuarioRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(Usuario usuario)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO ""Users"" (""Id"", ""SpotifyId"", ""DisplayName"", ""Email"", ""Provider"", ""PasswordHash"", ""SwipeCount"", ""Mood"", ""CreatedAt"", ""RefreshToken"", ""RefreshTokenExpiryTime"")
            VALUES (@Id, @SpotifyId, @DisplayName, @Email, @Provider, @PasswordHash, @SwipeCount, @Mood, @CreatedAt, @RefreshToken, @RefreshTokenExpiryTime)
            RETURNING ""Id""";
        
        return await connection.ExecuteScalarAsync<Guid>(sql, usuario);
    }

    public async Task<bool> UpdateAsync(Usuario usuario)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE ""Users""
            SET ""SpotifyId"" = @SpotifyId, 
                ""DisplayName"" = @DisplayName, 
                ""Email"" = @Email, 
                ""Provider"" = @Provider, 
                ""PasswordHash"" = @PasswordHash, 
                ""SwipeCount"" = @SwipeCount, 
                ""Mood"" = @Mood, 
                ""RefreshToken"" = @RefreshToken, 
                ""RefreshTokenExpiryTime"" = @RefreshTokenExpiryTime
            WHERE ""Id"" = @Id";
        
        var affectedRows = await connection.ExecuteAsync(sql, usuario);
        return affectedRows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM \"Users\" WHERE \"Id\" = @Id";
        var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
        return affectedRows > 0;
    }
}
