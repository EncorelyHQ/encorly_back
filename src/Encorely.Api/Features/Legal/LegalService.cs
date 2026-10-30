using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Legal;

public sealed class LegalService(AuthService auth, UserService users, IOptions<LegalOptions> options)
{
    public LegalResponse Current(HttpRequest request)
    {
        var o = options.Value;
        // Sin base configurada se usa la que el cliente ya alcanzó (p. ej. http://10.0.2.2:5000 desde el emulador).
        var baseUrl = string.IsNullOrWhiteSpace(o.PublicBaseUrl) ? $"{request.Scheme}://{request.Host}" : o.PublicBaseUrl.TrimEnd('/');
        return new LegalResponse(o.ConsentVersion, o.EffectiveDate, $"{baseUrl}/legal/terminos", $"{baseUrl}/legal/privacidad", $"{baseUrl}/cuenta/eliminar",
            string.IsNullOrWhiteSpace(o.PrivacyContactEmail) ? null : o.PrivacyContactEmail);
    }

    public async Task DeleteAccountAsync(AccountDeletionRequest request, CancellationToken ct)
    {
        // Mismo control que el login (bloqueo por intentos incluido): si no, esta página sería un oráculo de contraseñas sin freno.
        var user = await auth.VerifyCredentialsAsync(request.Email, request.Password, ct);
        await users.DeleteAsync(user.Id, ct);
    }
}
