using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace Encorely.Api.Infrastructure;

public static class OpenApiSetup
{
    public const string DocumentName = "v1";

    public static IServiceCollection AddEncorelyOpenApi(this IServiceCollection services) => services.AddOpenApi(DocumentName, o =>
    {
        o.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Info = new OpenApiInfo
            {
                Title = "Encorely API",
                Version = "1.0.0",
                Description = """
                    Contrato v1 del back de Encorely (.NET 10). Base: /api/v1. JSON camelCase; enums como string camelCase;
                    fechas ISO-8601 UTC; binarios en base64 estándar.

                    Errores: application/problem+json con `title`/`detail` en español, `errors` {campo: [mensajes]} en validación
                    y la extensión `code` (string estable para ramificar en el cliente) + `traceId`.

                    Identidad: SIEMPRE el `sub` del JWT (Authorization: Bearer). Ningún endpoint acepta userId del cliente para
                    decidir quién eres. Recursos ajenos responden 404.

                    Tiempo real: SignalR en /hubs/chat?access_token=<jwt>. Eventos servidor→cliente: MessageAvailable {matchId,id,seq},
                    PeerKeyChanged {matchId,userId}, MatchClosed {matchId}, MatchCreated {matchId}. El hub no tiene métodos invocables.
                    """,
            };
            doc.Servers = [];
            doc.Components ??= new OpenApiComponents();
            doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token de /auth/login, /auth/register o /auth/refresh.",
            };
            return Task.CompletedTask;
        });

        // Las extensiones que agrega Problems.cs, para que el cliente las tipe.
        o.AddSchemaTransformer((schema, ctx, _) =>
        {
            if (ctx.JsonTypeInfo.Type != typeof(Microsoft.AspNetCore.Mvc.ProblemDetails)) return Task.CompletedTask;
            schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            schema.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Código estable del error (p. ej. radar_locked, stale_recipient_key)." };
            schema.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String };
            schema.Properties["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = "Solo en validación: campo → mensajes en español.",
                AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
            };
            return Task.CompletedTask;
        });

        o.AddOperationTransformer((op, ctx, _) =>
        {
            var metadata = ctx.Description.ActionDescriptor.EndpointMetadata;
            var anonymous = metadata.OfType<IAllowAnonymous>().Any();
            if (!anonymous)
                op.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", ctx.Document)] = [] }];
            return Task.CompletedTask;
        });
    });
}
