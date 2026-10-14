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

        return BuildResponse(user, refresh.Token);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = Text.NormalizeEmail(request.Email);
        var user = await db.Users.Include(u => u.Genres).FirstOrDefaultAsync(u => u.Email == email, ct);
        var now = clock.GetUtcNow();

        if (user?.LockoutEndsAt > now)
            throw new ApiException(StatusCodes.Status429TooManyRequests, ErrorCodes.AccountLocked, "Demasiados intentos. Intenta de nuevo en unos minutos.");

        if (!hasher.Verify(request.Password, user?.PasswordHash))
        {
            if (user is not null) await RegisterFailureAsync(user, now, ct);
            throw ApiException.Unauthorized(ErrorCodes.InvalidCredentials, "Correo o contraseña incorrectos.");
        }

        if (user!.Status == UserStatus.Banned) throw Banned();

        user.FailedLoginAttempts = 0;
        user.LockoutEndsAt = null;
        var refresh = NewRefreshToken(user.Id, Guid.CreateVersion7(), now);
        db.RefreshTokens.Add(refresh.Entity);
        await db.SaveChangesAsync(ct);

        return BuildResponse(user, refresh.Token);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = TokenService.HashRefreshToken(request.RefreshToken);
        var current = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (current is null || current.ExpiresAt <= now) throw InvalidRefresh();

        if (current.RevokedAt is not null || current.ReplacedByTokenId is not null)
        {
            await RevokeFamilyAsync(current.FamilyId, now, ct);
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
            await RevokeFamilyAsync(current.FamilyId, now, ct);
            throw InvalidRefresh();
        }

        db.RefreshTokens.Add(next.Entity);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return BuildResponse(user, next.Token);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct)
    {
        var hash = TokenService.HashRefreshToken(request.RefreshToken);
        var familyId = await db.RefreshTokens.Where(t => t.TokenHash == hash).Select(t => (Guid?)t.FamilyId).FirstOrDefaultAsync(ct);
        if (familyId is { } id) await RevokeFamilyAsync(id, clock.GetUtcNow(), ct);
    }

    private async Task RegisterFailureAsync(User user, DateTimeOffset now, CancellationToken ct)
    {
        user.FailedLoginAttempts++;
        if (user.FailedLoginAttempts >= limits.Value.MaxFailedLogins)
        {
            user.LockoutEndsAt = now.AddMinutes(limits.Value.LockoutMinutes);
            user.FailedLoginAttempts = 0;
        }
        await db.SaveChangesAsync(ct);
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct)
        => db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

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

    private AuthResponse BuildResponse(User user, string refreshToken)
    {
        var (access, expiresIn) = tokens.CreateAccessToken(user);
        return new AuthResponse(access, refreshToken, expiresIn,
            new AuthUserDto(user.Id, user.Email, user.DisplayName, user.OnboardingCompleted, user.Role));
    }

    private static ApiException EmailTaken() => ApiException.Conflict(ErrorCodes.EmailTaken, "Ya existe una cuenta con ese correo.");
    private static ApiException InvalidRefresh() => ApiException.Unauthorized(ErrorCodes.InvalidRefreshToken, "Tu sesión expiró. Inicia sesión de nuevo.");
    private static ApiException Banned() => ApiException.Forbidden(ErrorCodes.AccountBanned, "Tu cuenta está suspendida.");
}
