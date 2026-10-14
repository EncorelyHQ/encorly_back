using Encorely.Api.Infrastructure.Configuration;
using Encorely.Api.Infrastructure.Database;
using Encorely.Api.Infrastructure.Realtime;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Users;

public sealed class UserService(AppDbContext db, Notifier notifier, IOptions<LegalOptions> legal, TimeProvider clock)
{
    public Task<MeResponse> GetAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
    public Task<MeResponse> UpdateAsync(Guid userId, UpdateMeRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task DeleteAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
    public Task AcceptConsentAsync(Guid userId, ConsentRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<DataExportResponse> ExportAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
}
