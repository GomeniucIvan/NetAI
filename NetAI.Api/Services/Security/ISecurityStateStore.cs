using System.Threading;

namespace NetAI.Api.Services.Security;

public interface ISecurityStateStore
{
    Task<SecurityStateRecord> LoadAsync(CancellationToken cancellationToken = default);

    Task StoreAsync(SecurityStateRecord state, CancellationToken cancellationToken = default);
}
