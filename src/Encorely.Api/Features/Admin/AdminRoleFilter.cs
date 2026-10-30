using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Api.Features.Admin;

/// <summary>
/// El claim de rol del JWT puede quedar viejo hasta 15 min: para administración se confirma el rol en la BD
/// en cada request (quitar el rol corta el acceso de inmediato).
/// </summary>
public sealed class AdminRoleFilter(AppDbContext db) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var userId = context.HttpContext.User.UserId();
        var isAdmin = await db.Users.AnyAsync(u => u.Id == userId && u.Role == UserRole.Admin, context.HttpContext.RequestAborted);
        if (!isAdmin) throw ApiException.Forbidden(ErrorCodes.Forbidden, "No tienes permiso para esta acción.");
        return await next(context);
    }
}
