using System.Threading;
using System.Threading.Tasks;
using NetAI.Api.Models.Experiments;

namespace NetAI.Api.Data.Repositories;

public interface IExperimentConfigRepository
{
    Task<bool> ExistsAsync(string conversationId, CancellationToken cancellationToken);

    Task WriteAsync(string conversationId, ExperimentConfigDto config, CancellationToken cancellationToken);
}
