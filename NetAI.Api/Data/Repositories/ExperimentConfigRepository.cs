using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetAI.Api.Models.Experiments;

namespace NetAI.Api.Data.Repositories;

public class ExperimentConfigRepository : IExperimentConfigRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    private readonly ILogger<ExperimentConfigRepository> _logger;

    public ExperimentConfigRepository(ILogger<ExperimentConfigRepository> logger)
    {
        _logger = logger;
    }

    public Task<bool> ExistsAsync(string conversationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException("Conversation identifier is required", nameof(conversationId));
        }

        string filePath = GetFilePath(conversationId);
        bool exists = File.Exists(filePath);
        return Task.FromResult(exists);
    }

    public async Task WriteAsync(string conversationId, ExperimentConfigDto config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException("Conversation identifier is required", nameof(conversationId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        string filePath = GetFilePath(conversationId);
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            await using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, config, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write experiment config to {FilePath}", filePath);
            throw;
        }
    }

    private static string GetFilePath(string conversationId)
    {
        string safeConversationId = Path.GetFileName(conversationId);
        string baseDirectory = AppContext.BaseDirectory;
        return Path.Combine(baseDirectory, "data", "conversations", safeConversationId, "exp_config.json");
    }
}
