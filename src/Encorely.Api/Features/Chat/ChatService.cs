using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Realtime;

namespace Encorely.Api.Features.Chat;

/// <summary>El servidor NUNCA descifra ni registra en logs contenido de mensajes: solo ids, seq y tamaños.</summary>
public sealed class ChatService(AppDbContext db, Notifier notifier, TimeProvider clock)
{
    public Task<(MessageAcceptedDto Accepted, bool Created)> SendAsync(Guid userId, Guid matchId, SendMessageRequest request, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<MessagePage> ListAsync(Guid userId, Guid matchId, long? before, long? after, int limit, CancellationToken ct)
        => throw new NotImplementedException();
    public Task AckAsync(Guid userId, Guid matchId, AckRequest request, CancellationToken ct) => throw new NotImplementedException();
}
