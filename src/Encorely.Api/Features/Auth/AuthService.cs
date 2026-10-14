using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Encorely.Api.Features.Auth;

public sealed class AuthService(
    AppDbContext db,
    PasswordHasher hasher,
    TokenService tokens,
    SessionRevoker sessions,
    IOptions<JwtOptions> jwt,
    IOptions<RateLimitOptions> limits,
    IOptions<LegalOptions> legal,
    TimeProvider clock)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (!request.AcceptTerms || request.ConsentVersion != legal.Value.ConsentVersion)
            throw ApiException.BadRequest(ErrorCodes.ConsentRequired, "Debes aceptar los términos y la política de datos vigentes.");

        var email = Text.NormalizeEmail(request.Email);
        var displayName = request.DisplayName.Trim();
        if (displayName.Length < 2)
            throw ApiException.BadRequest(ErrorCodes.ValidationFailed, "Revisa los datos enviados.", "El nombre debe tener al menos 2 caracteres.");

        if (await db.Users.AnyAsync(u => u.Email == email, ct)) throw EmailTaken();
        // Después del 409: un correo con cuenta (aunque suspendida) sigue respondiendo igual que antes, sin revelar el ban.
        var emailHash = BannedEmail.HashOf(email);
        if (await db.BannedEmails.AnyAsync(b => b.EmailHash == emailHash, ct))
            throw ApiException.Forbidden(ErrorCodes.AccountBanned, "No puedes crear una cuenta con este correo.");

        var now = clock.GetUtcNow();
        var user = new User
        {
            Email = email,
            PasswordHash = hasher.Hash(request.Password),
            DisplayName = displayName,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Users.Add(user);
        db.UserConsents.Add(new UserConsent { UserId = user.Id, Version = request.ConsentVersion, AcceptedAt = now });
        var refresh = NewRefreshToken(user.Id, Guid.CreateVersion7(), now);
        db.RefreshTokens.Add(refresh.Entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Carrera: otro registro con el mismo email ganó entre el AnyAsync y el INSERT.
            throw EmailTaken();
        }

        return BuildResponse(user, refresh.Token, refresh.Entity.FamilyId);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await VerifyCredentialsAsync(request.Email, request.Password, ct);
        if (user.Status == UserStatus.Banned) throw Banned();

        var refresh = NewRefreshToken(user.Id, Guid.CreateVersion7(), clock.GetUtcNow());
        db.RefreshTokens.Add(refresh.Entity);
        await db.SaveChangesAsync(ct);

        return BuildResponse(user, refresh.Token, refresh.Entity.FamilyId);
    }

    /// <summary>
    /// Única puerta para comprobar correo + contraseña (login y borrado web de cuenta): aplica el bloqueo por intentos
    /// (429), compara contra un hash ficticio si la cuenta no existe, cuenta el fallo de forma atómica y reinicia el
    /// contador al acertar. No mira el estado de la cuenta: un suspendido puede borrarla (Ley 1581).
    /// </summary>
    public async Task<User> VerifyCredentialsAsync(string rawEmail, string password, CancellationToken ct)
    {
        var email = Text.NormalizeEmail(rawEmail);
        var user = await db.Users.AsNoTracking().Include(u => u.Genres).FirstOrDefaultAsync(u => u.Email == email, ct);
        return await VerifyAsync(user, password,
            () => ApiException.Unauthorized(ErrorCodes.InvalidCredentials, "Correo o contraseña incorrectos."), ct);
    }

    /// <summary>
    /// Reconfirma la contraseña del usuario ya autenticado (operaciones sensibles), con el mismo bloqueo por intentos.
    /// Falla con 403 y no 401: la sesión sigue siendo válida y el cliente no debe intentar refrescarla ni cerrarla.
    /// </summary>
    public async Task VerifyPasswordAsync(Guid userId, string password, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        await VerifyAsync(user, password, () => ApiException.Forbidden(ErrorCodes.InvalidPassword, "La contraseña no es correcta."), ct);
    }

    private async Task<User> VerifyAsync(User? user, string password, Func<ApiException> wrongPassword, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        if (user?.LockoutEndsAt > now)
            throw new ApiException(StatusCodes.Status429TooManyRequests, ErrorCodes.AccountLocked, "Demasiados intentos. Intenta de nuevo en unos minutos.");

        if (!hasher.Verify(password, user?.PasswordHash))
        {
            if (user is not null) await RegisterFailureAsync(user.Id, now, ct);
            throw wrongPassword();
        }

        if (user!.FailedLoginAttempts != 0 || user.LockoutEndsAt is not null)
            await db.Users.Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FailedLoginAttempts, 0).SetProperty(u => u.LockoutEndsAt, (DateTimeOffset?)null), ct);
        return user;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = TokenService.HashRefreshToken(request.RefreshToken);
        var current = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (current is null || current.ExpiresAt <= now) throw InvalidRefresh();

        if (current.RevokedAt is not null || current.ReplacedByTokenId is not null)
        {
            await sessions.RevokeAsync(current.UserId, current.FamilyId, ct);
            throw InvalidRefresh();
        }

        var user = await db.Users.Include(u => u.Genres).FirstOrDefaultAsync(u => u.Id == current.UserId, ct) ?? throw InvalidRefresh();
        if (user.Status == UserStatus.Banned) throw Banned();

        var next = NewRefreshToken(user.Id, current.FamilyId, now);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Condicionado a RevokedAt IS NULL: de dos refresh concurrentes con el mismo token solo uno rota.
        var rotated = await db.RefreshTokens
            .Where(t => t.Id == current.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.ReplacedByTokenId, next.Entity.Id), ct);
        if (rotated != 1)
        {
            await tx.RollbackAsync(ct);
            await sessions.RevokeAsync(current.UserId, current.FamilyId, ct);
            throw InvalidRefresh();
        }

        db.RefreshTokens.Add(next.Entity);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return BuildResponse(user, next.Token, current.FamilyId);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct)
    {
        var hash = TokenService.HashRefreshToken(request.RefreshToken);
        var session = await db.RefreshTokens.Where(t => t.TokenHash == hash).Select(t => new { t.UserId, t.FamilyId }).FirstOrDefaultAsync(ct);
        if (session is not null) await sessions.RevokeAsync(session.UserId, session.FamilyId, ct);
    }

    /// <summary>
    /// Un solo UPDATE: incrementar sobre la entidad perdía fallos en paralelo (cada petición leía el mismo contador).
    /// Al llegar al máximo fija el bloqueo y reinicia el contador en la misma sentencia.
    /// </summary>
    private async Task RegisterFailureAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var max = limits.Value.MaxFailedLogins;
        var lockUntil = now.AddMinutes(limits.Value.LockoutMinutes);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE users SET
                lockout_ends_at = CASE WHEN failed_login_attempts + 1 >= {max} THEN {lockUntil} ELSE lockout_ends_at END,
                failed_login_attempts = CASE WHEN failed_login_attempts + 1 >= {max} THEN 0 ELSE failed_login_attempts + 1 END
            WHERE id = {userId}
            """, ct);
    }

    private (string Token, RefreshToken Entity) NewRefreshToken(Guid userId, Guid familyId, DateTimeOffset now)
    {
        var (token, hash) = TokenService.CreateRefreshToken();
        return (token, new RefreshToken
        {
            UserId = userId,
            FamilyId = familyId,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwt.Value.RefreshTokenDays),
        });
    }

    private AuthResponse BuildResponse(User user, string refreshToken, Guid familyId)
    {
        var (access, expiresIn) = tokens.CreateAccessToken(user, familyId);
        return new AuthResponse(access, refreshToken, expiresIn,
            new AuthUserDto(user.Id, user.Email, user.DisplayName, user.OnboardingCompleted, user.Role));
    }

    private static ApiException EmailTaken() => ApiException.Conflict(ErrorCodes.EmailTaken, "Ya existe una cuenta con ese correo.");
    private static ApiException InvalidRefresh() => ApiException.Unauthorized(ErrorCodes.InvalidRefreshToken, "Tu sesión expiró. Inicia sesión de nuevo.");
    private static ApiException Banned() => ApiException.Forbidden(ErrorCodes.AccountBanned, "Tu cuenta está suspendida.");
}
