using System.Net;
using System.Text.Json;
using EncorelyApplication.Exceptions;

namespace EncorelyApi.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, message) = ex switch
            {
                NotFoundException e         => (HttpStatusCode.NotFound, e.Message),
                DuplicateEmailException e   => (HttpStatusCode.Conflict, e.Message),
                InvalidCredentialsException e => (HttpStatusCode.Unauthorized, e.Message),
                _                          => (HttpStatusCode.InternalServerError, "Internal Server Error")
            };

            if (statusCode == HttpStatusCode.InternalServerError)
                _logger.LogError(ex, ex.Message);
            else
                _logger.LogWarning(ex.Message);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var body = _env.IsDevelopment() && statusCode == HttpStatusCode.InternalServerError
                ? (object)new { statusCode = context.Response.StatusCode, message = ex.Message, stackTrace = ex.StackTrace?.ToString() }
                : (object)new { statusCode = context.Response.StatusCode, message };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, options));
        }
    }
}
