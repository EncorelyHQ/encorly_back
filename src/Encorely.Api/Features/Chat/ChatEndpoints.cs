using System.ComponentModel;
using System.Security.Claims;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Encorely.Api.Features.Chat;

public static class ChatEndpoints
{
    public static void MapChat(this IEndpointRouteBuilder api)
    {
        var keys = api.MapGroup("").WithTags("Llaves E2EE");
        keys.MapPut("/keys/me", PutMyKeys).WithName("PutMyKeys")
            .WithSummary("Publica o rota mis llaves públicas. Si cambia la identidad, se avisa a mis matches (PeerKeyChanged)")
            .Problems(400, 401, 409);
        keys.MapGet("/keys/me", GetMyKeys).WithName("GetMyKeys").WithSummary("Mis llaves públicas vigentes (para saber el último boxKeyId)").Problems(401, 404);
        keys.MapGet("/matches/{id:guid}/peer-keys", GetPeerKeys).WithName("GetPeerKeys")
            .WithSummary("Llaves públicas vigentes de la otra persona del match").Problems(401, 404);
        keys.MapGet("/matches/{id:guid}/peer-keys/box/{boxKeyId:int}", GetPeerBoxKey).WithName("GetPeerBoxKey")
            .WithSummary("Una llave de caja histórica de la otra persona (para descifrar mensajes viejos)").Problems(401, 404);

        var chat = api.MapGroup("/matches/{id:guid}/messages").WithTags("Chat");
        chat.MapPost("", Send).WithName("SendMessage")
            .WithSummary("Envía un mensaje cifrado. 201 al crearlo; 200 si el mismo id ya existía con el mismo contenido")
            .Produces<MessageAcceptedDto>(StatusCodes.Status200OK)
            .Problems(400, 401, 403, 404, 409);
        chat.MapGet("", List).WithName("ListMessages")
            .WithSummary("Mensajes cifrados del match. ?before=seq pagina hacia atrás (desc); ?after=seq trae los nuevos (asc)")
            .Problems(400, 401, 404);
        chat.MapPost("/ack", Ack).WithName("AckMessages")
            .WithSummary("Confirma que guardé localmente los mensajes recibidos hasta upToSeq; el servidor los borra")
            .Problems(400, 401, 404);
    }

    private static async Task<Ok<KeyBundleDto>> PutMyKeys(UploadKeysRequest request, ClaimsPrincipal user, KeyService keys, CancellationToken ct)
        => TypedResults.Ok(await keys.PutAsync(user.UserId(), request, ct));

    private static async Task<Ok<KeyBundleDto>> GetMyKeys(ClaimsPrincipal user, KeyService keys, CancellationToken ct)
        => TypedResults.Ok(await keys.GetMineAsync(user.UserId(), ct));

    private static async Task<Ok<KeyBundleDto>> GetPeerKeys(Guid id, ClaimsPrincipal user, KeyService keys, CancellationToken ct)
        => TypedResults.Ok(await keys.GetPeerAsync(user.UserId(), id, ct));

    private static async Task<Ok<BoxKeyDto>> GetPeerBoxKey(Guid id, int boxKeyId, ClaimsPrincipal user, KeyService keys, CancellationToken ct)
        => TypedResults.Ok(await keys.GetPeerBoxKeyAsync(user.UserId(), id, boxKeyId, ct));

    private static async Task<Results<Created<MessageAcceptedDto>, Ok<MessageAcceptedDto>>> Send(
        Guid id, SendMessageRequest request, ClaimsPrincipal user, ChatService chat, CancellationToken ct)
    {
        var (accepted, created) = await chat.SendAsync(user.UserId(), id, request, ct);
        return created
            ? TypedResults.Created($"/api/v1/matches/{id}/messages?after={accepted.Seq - 1}", accepted)
            : TypedResults.Ok(accepted);
    }

    private static async Task<Ok<MessagePage>> List(
        Guid id, ClaimsPrincipal user, ChatService chat, CancellationToken ct,
        [Description("Trae mensajes con seq menor (desc). Excluyente con after")] long? before = null,
        [Description("Trae mensajes con seq mayor (asc). Excluyente con before")] long? after = null,
        [Description("1–100, por defecto 50")] int limit = 50)
        => TypedResults.Ok(await chat.ListAsync(user.UserId(), id, before, after, limit, ct));

    private static async Task<NoContent> Ack(Guid id, AckRequest request, ClaimsPrincipal user, ChatService chat, CancellationToken ct)
    {
        await chat.AckAsync(user.UserId(), id, request, ct);
        return TypedResults.NoContent();
    }
}
