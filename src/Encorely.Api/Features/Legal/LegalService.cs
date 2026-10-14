using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Legal;

public sealed class LegalService(AppDbContext db, PasswordHasher hasher, UserService users, IOptions<LegalOptions> options)
{
    public LegalResponse Current() => throw new NotImplementedException();
    public Task DeleteAccountAsync(AccountDeletionRequest request, CancellationToken ct) => throw new NotImplementedException();
}
