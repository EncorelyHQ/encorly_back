namespace Encorely.Api.Infrastructure.Http;

/// <summary>Mensajes de validación en español ({1}, {2}: límites del atributo).</summary>
public static class Msg
{
    public const string Required = "Es obligatorio.";
    public const string Email = "No es un correo válido.";
    public const string MaxLength = "No puede superar {1} caracteres.";
    public const string MaxItems = "No puede tener más de {1} elementos.";
    public const string Length = "Debe tener entre {2} y {1} caracteres.";
    public const string Range = "Debe estar entre {1} y {2}.";
    public const string PasswordLength = "Debe tener al menos {1} caracteres.";
}

public static class EndpointExtensions
{
    public const string UserWritesPolicy = "user-writes";
    public const string ReportsPolicy = "reports";
    public const string TokensPolicy = "tokens";
    /// <summary>Tope de cuerpo por defecto: cubre un mensaje de chat de 64 KiB en base64 con margen.</summary>
    public const long DefaultMaxBodyBytes = 256 * 1024;

    /// <summary>Límite por usuario (no por IP) para escrituras que se prestan a abuso.</summary>
    public static RouteHandlerBuilder UserWrites(this RouteHandlerBuilder builder)
        => builder.RequireRateLimiting(UserWritesPolicy).ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <summary>Sube el tope de cuerpo de este endpoint por encima de <see cref="DefaultMaxBodyBytes"/>.</summary>
    public static RouteHandlerBuilder MaxBody(this RouteHandlerBuilder builder, long bytes)
        => builder.WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(bytes)).ProducesProblem(StatusCodes.Status413PayloadTooLarge);

    /// <summary>Documenta en el OpenAPI las respuestas ProblemDetails posibles.</summary>
    public static RouteHandlerBuilder Problems(this RouteHandlerBuilder builder, params int[] statuses)
    {
        foreach (var status in statuses) builder.ProducesProblem(status);
        return builder;
    }
}
