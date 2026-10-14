using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Legal;

public sealed class LegalService(AuthService auth, UserService users, IOptions<LegalOptions> options)
{
    public LegalResponse Current()
    {
        var o = options.Value;
        var baseUrl = o.PublicBaseUrl.TrimEnd('/');
        return new LegalResponse(o.ConsentVersion, o.EffectiveDate, $"{baseUrl}/legal/terminos", $"{baseUrl}/legal/privacidad", $"{baseUrl}/cuenta/eliminar");
    }

    public async Task DeleteAccountAsync(AccountDeletionRequest request, CancellationToken ct)
    {
        // Mismo control que el login (bloqueo por intentos incluido): si no, esta página sería un oráculo de contraseñas sin freno.
        var user = await auth.VerifyCredentialsAsync(request.Email, request.Password, ct);
        await users.DeleteAsync(user.Id, ct);
    }
}
