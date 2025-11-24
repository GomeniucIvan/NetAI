using System.Threading;
using System.Threading.Tasks;
using Octokit;

namespace NetAI.Api.Services.Git;

public interface IGitHubClientFactory
{
    Task<IGitHubClient> CreateClientAsync(CancellationToken cancellationToken = default);
}
