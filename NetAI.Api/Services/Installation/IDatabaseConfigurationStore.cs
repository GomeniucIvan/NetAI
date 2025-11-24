using System.Threading;
using System.Threading.Tasks;

namespace NetAI.Api.Services.Installation;

public interface IDatabaseConfigurationStore
{
    Task<string> LoadConnectionStringAsync(CancellationToken cancellationToken = default);

    Task StoreConnectionStringAsync(string connectionString, CancellationToken cancellationToken = default);
}
