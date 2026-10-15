using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

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
        [405] = ErrorCodes.MethodNotAllowed,
        [409] = ErrorCodes.Conflict,
        [413] = ErrorCodes.PayloadTooLarge,
        [415] = ErrorCodes.UnsupportedMediaType,
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
            else if (IsFrameworkTitle(pd.Title, status))
            {
                // Los títulos por defecto del framework vienen en inglés; los de ApiException se respetan.
                pd.Title = SpanishTitles.GetValueOrDefault(status, pd.Title ?? "Error");
            }

            pd.Type = null;
            pd.Instance = null;
            if (status >= 500 && ctx.Exception is not ApiException) pd.Detail = null;
            if (DefaultCodes.TryGetValue(status, out var code)) pd.Extensions.TryAdd("code", code);
            pd.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
            pd.Extensions.Remove("exception");
        });
        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }

    // ReasonPhrases todavía usa los nombres de RFC 7231; ProblemDetails y Kestrel ya usan los de RFC 9110.
    private static readonly Dictionary<int, string> Rfc9110Titles = new() { [413] = "Content Too Large", [422] = "Unprocessable Content" };

    private static bool IsFrameworkTitle(string? title, int status) => title is null
        || title == Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status)
        || title == Rfc9110Titles.GetValueOrDefault(status)
        || title.StartsWith("One or more validation errors", StringComparison.Ordinal)
        || title.StartsWith("An error occurred", StringComparison.Ordinal);

    private static string CamelPath(string path) => string.Join('.', path.Split('.')
        .Select(segment => segment.Length > 0 ? char.ToLowerInvariant(segment[0]) + segment[1..] : segment));

    public static string TitleFor(int status) => SpanishTitles.GetValueOrDefault(status, "Error");
}

internal sealed partial class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
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
                    Detail = bad.StatusCode switch
                    {
                        400 => BindingField(bad) is { } field
                            ? $"Revisa el campo {field}: falta o no tiene el formato esperado."
                            : "El cuerpo o los parámetros de la solicitud no tienen el formato esperado.",
                        413 => "Reduce el tamaño de lo que envías.",
                        _ => null,
                    },
                };
                break;
            case var ex when IsInvalidText(ex):
                // Red de seguridad para query strings/route con U+0000 que el converter de JSON no ve.
                pd = new ProblemDetails
                {
                    Status = 400,
                    Title = Problems.TitleFor(400),
                    Detail = "Los datos enviados contienen caracteres no válidos.",
                };
                pd.Extensions["code"] = ErrorCodes.InvalidBody;
                break;
            case OperationCanceledException when http.RequestAborted.IsCancellationRequested:
                return true;
            default:
                logger.LogError(exception, "Error no controlado en {Method} {Path} [{TraceId}]", http.Request.Method, http.Request.Path, http.TraceIdentifier);
                pd = new ProblemDetails { Status = 500, Title = Problems.TitleFor(500) };
                break;
        }

        http.Response.StatusCode = pd.Status!.Value;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = pd, Exception = exception });
    }

    /// <summary>
    /// El campo que falló, sin datos del valor: la ruta JSON del cuerpo (<c>kdf.opsLimit</c>) o el nombre del parámetro de
    /// query o ruta (<c>limit</c>). null si el JSON está mal formado desde la raíz o el framework no lo dice.
    /// </summary>
    internal static string? BindingField(BadHttpRequestException bad)
    {
        for (Exception? e = bad.InnerException; e is not null; e = e.InnerException)
            if (e is System.Text.Json.JsonException { Path: { Length: > 2 } path } && path.StartsWith("$.", StringComparison.Ordinal))
                return path[2..];
        // 'Failed to bind parameter "int limit" from "abc".' / 'Required parameter "string status" was not provided from query string.'
        var m = BindingParameter().Match(bad.Message);
        return m.Success && !bad.Message.Contains("from body", StringComparison.Ordinal) && bad.InnerException is not System.Text.Json.JsonException
            ? m.Groups["name"].Value
            : null;
    }

    [System.Text.RegularExpressions.GeneratedRegex("parameter \"(?:[^\" ]+ )?(?<name>[A-Za-z_][A-Za-z0-9_]*)\"")]
    private static partial System.Text.RegularExpressions.Regex BindingParameter();

    // 22021: byte inválido para UTF-8 (U+0000). 22P05: escape Unicode no soportado en jsonb. Puede venir envuelta (DbUpdateException).
    private static bool IsInvalidText(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
            if (e is PostgresException { SqlState: "22021" or "22P05" }) return true;
        return false;
    }
}
