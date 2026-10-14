using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Auth;

public sealed class AuthService(
    AppDbContext db,
    PasswordHasher hasher,
    TokenService tokens,
    IOptions<JwtOptions> jwt,
    IOptions<RateLimitOptions> limits,
    IOptions<LegalOptions> legal,
    TimeProvider clock)
{
    public Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task LogoutAsync(LogoutRequest request, CancellationToken ct) => throw new NotImplementedException();
}
