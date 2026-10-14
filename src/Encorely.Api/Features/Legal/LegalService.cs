using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Legal;

public sealed class LegalService(AppDbContext db, PasswordHasher hasher, UserService users, IOptions<LegalOptions> options)
{
    public LegalResponse Current()
    {
        var o = options.Value;
        var baseUrl = o.PublicBaseUrl.TrimEnd('/');
        return new LegalResponse(o.ConsentVersion, o.EffectiveDate, $"{baseUrl}/legal/terminos", $"{baseUrl}/legal/privacidad", $"{baseUrl}/cuenta/eliminar");
    }

    public async Task DeleteAccountAsync(AccountDeletionRequest request, CancellationToken ct)
    {
        var email = Text.NormalizeEmail(request.Email);
        var user = await db.Users.AsNoTracking().Where(u => u.Email == email).Select(u => new { u.Id, u.PasswordHash }).FirstOrDefaultAsync(ct);

        if (!hasher.Verify(request.Password, user?.PasswordHash))
            throw ApiException.Unauthorized(ErrorCodes.InvalidCredentials, "Correo o contraseña incorrectos.");

        await users.DeleteAsync(user!.Id, ct);
    }
}
