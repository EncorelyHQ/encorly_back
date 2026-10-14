using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Encorely.Api.Infrastructure.Http;

public static class Problems
{
    private static readonly Dictionary<int, string> SpanishTitles = new()
    {
        [400] = "La solicitud no es válida.",
        [401] = "Necesitas iniciar sesión.",
        [403] = "No tienes permiso para esta acción.",
        [404] = "No encontramos lo que buscas.",
        [405] = "Método no permitido.",
        [409] = "Hay un conflicto con el estado actual.",
        [413] = "La solicitud es demasiado grande.",
        [415] = "Tipo de contenido no soportado.",
        [429] = "Demasiadas solicitudes. Intenta de nuevo en un momento.",
        [500] = "Ocurrió un error inesperado.",
        [503] = "El servicio no está disponible.",
    };

    private static readonly Dictionary<int, string> DefaultCodes = new()
    {
        [400] = ErrorCodes.InvalidBody,
        [401] = ErrorCodes.Unauthorized,
        [403] = ErrorCodes.Forbidden,
        [404] = ErrorCodes.NotFound,
        [429] = ErrorCodes.RateLimited,
        [500] = ErrorCodes.Internal,
    };

    public static IServiceCollection AddEncorelyProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
        {
            var pd = ctx.ProblemDetails;
            var status = pd.Status ?? ctx.HttpContext.Response.StatusCode;

            if (pd is HttpValidationProblemDetails { Errors.Count: > 0 } validation)
            {
                // Las claves deben coincidir con los nombres JSON (camelCase), también en rutas anidadas: topArtists[0].name.
                var camel = validation.Errors.ToDictionary(kv => CamelPath(kv.Key), kv => kv.Value);
                validation.Errors.Clear();
                foreach (var (key, messages) in camel) validation.Errors[key] = messages;
                pd.Title = "Revisa los datos enviados.";
                pd.Extensions.TryAdd("code", ErrorCodes.ValidationFailed);
            }
            else if (pd.Title is null || pd.Title.All(c => c < 128))
            {
                // Los títulos por defecto del framework vienen en inglés.
                pd.Title = SpanishTitles.GetValueOrDefault(status, pd.Title ?? "Error");
            }

            pd.Type = null;
            pd.Instance = null;
            if (pd.Detail is not null && pd.Detail.All(c => c < 128) && status >= 500) pd.Detail = null;
            if (DefaultCodes.TryGetValue(status, out var code)) pd.Extensions.TryAdd("code", code);
            pd.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
            pd.Extensions.Remove("exception");
        });
        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }

    private static string CamelPath(string path) => string.Join('.', path.Split('.')
        .Select(segment => segment.Length > 0 ? char.ToLowerInvariant(segment[0]) + segment[1..] : segment));

    public static string TitleFor(int status) => SpanishTitles.GetValueOrDefault(status, "Error");
}

internal sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        ProblemDetails pd;
        switch (exception)
        {
            case ApiException api:
                pd = new ProblemDetails { Status = api.Status, Title = api.Title, Detail = api.Detail };
                pd.Extensions["code"] = api.Code;
                break;
            case BadHttpRequestException bad:
                // JSON mal formado, enum desconocido, tipos incorrectos: siempre 400, nunca 500.
                pd = new ProblemDetails
                {
                    Status = bad.StatusCode,
                    Title = Problems.TitleFor(bad.StatusCode),
                    Detail = bad.StatusCode == 400 ? "El cuerpo o los parámetros de la solicitud no tienen el formato esperado." : null,
                };
                break;
            case OperationCanceledException when http.RequestAborted.IsCancellationRequested:
                return true;
            default:
                logger.LogError(exception, "Error no controlado en {Method} {Path}", http.Request.Method, http.Request.Path);
                pd = new ProblemDetails { Status = 500, Title = Problems.TitleFor(500) };
                break;
        }

        http.Response.StatusCode = pd.Status!.Value;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = pd, Exception = exception });
    }
}
