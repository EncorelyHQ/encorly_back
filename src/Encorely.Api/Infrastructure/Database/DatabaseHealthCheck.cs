using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Encorely.Api.Infrastructure.Database;

public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch
        {
            // Sin detalles: el mensaje de Npgsql revela host/usuario.
            return HealthCheckResult.Unhealthy();
        }
    }
}
