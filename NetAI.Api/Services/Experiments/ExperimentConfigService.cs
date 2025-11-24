using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetAI.Api.Data.Repositories;
using NetAI.Api.Models.Experiments;

namespace NetAI.Api.Services.Experiments;

public interface IExperimentConfigService
{
    Task<bool> StoreExperimentConfigAsync(string conversationId, ExperimentConfigDto config, CancellationToken cancellationToken);
}

public class ExperimentConfigService : IExperimentConfigService
{
    private readonly IExperimentConfigRepository _repository;
    private readonly ILogger<ExperimentConfigService> _logger;

    public ExperimentConfigService(IExperimentConfigRepository repository, ILogger<ExperimentConfigService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> StoreExperimentConfigAsync(string conversationId, ExperimentConfigDto config, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool exists = await _repository.ExistsAsync(conversationId, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            return false;
        }

        try
        {
            await _repository.WriteAsync(conversationId, config, cancellationToken).ConfigureAwait(false);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Failed to persist experiment config for conversation {ConversationId}", conversationId);
            return true;
        }
    }
}
