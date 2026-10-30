namespace Encorely.Api.Infrastructure.Auth;

public sealed class PasswordHasher
{
    private const int WorkFactor = 12;

    // Hash de una contraseña aleatoria: verificar contra él cuando el email no existe iguala los tiempos
    // de respuesta y evita el oráculo de enumeración (1,7 ms vs 136 ms en el legado).
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), WorkFactor);

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string? hash)
    {
        var ok = BCrypt.Net.BCrypt.Verify(password, hash ?? DummyHash);
        return hash is not null && ok;
    }
}
