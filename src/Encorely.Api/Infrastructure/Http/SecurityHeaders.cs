namespace Encorely.Api.Infrastructure.Http;

public static class SecurityHeaders
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h.XContentTypeOptions = "nosniff";
            h.XFrameOptions = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            // La API solo sirve JSON; las páginas HTML (swagger, borrado de cuenta) fijan su propia CSP.
            if (!h.ContainsKey("Content-Security-Policy"))
                h.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            return Task.CompletedTask;
        });
        await next();
    });
}

/// <summary>
/// Log de acceso propio: ruta SIN query string (el token del hub viaja en <c>?access_token=</c>)
/// ni cuerpos. El log de hosting de ASP.NET se baja a Warning en appsettings por la misma razón.
/// </summary>
public static class RequestLogging
{
    public static IApplicationBuilder UseSafeRequestLogging(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        int? failed = null;
        try
        {
            await next();
        }
        catch
        {
            // Si una excepción llega hasta aquí el servidor responderá 500, pero StatusCode todavía dice 200.
            failed = StatusCodes.Status500InternalServerError;
            throw;
        }
        finally
        {
            var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Encorely.Http");
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            logger.LogInformation("{Method} {Path} -> {Status} en {Elapsed:0.0} ms [{TraceId}]",
                ctx.Request.Method, ctx.Request.Path.Value, failed ?? ctx.Response.StatusCode, elapsed, ctx.TraceIdentifier);
        }
    });
}
