using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetAI.Api.Data.Entities.Sandboxes;
using NetAI.Api.Data.Repositories;
using NetAI.Api.Models.Sandboxes;

namespace NetAI.Api.Services.Sandboxes;

public class SandboxService : ISandboxService
{
    private const int DefaultLimit = 100;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ISandboxRepository _sandboxRepository;
    private readonly ISandboxSpecService _sandboxSpecService;
    private readonly ISandboxOrchestrationClient _sandboxOrchestrationClient;
    private readonly ILogger<SandboxService> _logger;

    public SandboxService(
        ISandboxRepository sandboxRepository,
        ISandboxSpecService sandboxSpecService,
        ISandboxOrchestrationClient sandboxOrchestrationClient,
        ILogger<SandboxService> logger)
    {
        _sandboxRepository = sandboxRepository;
        _sandboxSpecService = sandboxSpecService;
        _sandboxOrchestrationClient = sandboxOrchestrationClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SandboxInfoDto>> BatchGetSandboxesAsync(
        IReadOnlyList<string> sandboxIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, SandboxRecord> lookup = await _sandboxRepository
            .BatchGetAsync(sandboxIds, cancellationToken)
            .ConfigureAwait(false);

        return sandboxIds
            .Select(id => lookup.TryGetValue(id, out SandboxRecord record) ? MapSandbox(record) : null)
            .ToList();
    }

    public async Task<bool> DeleteSandboxAsync(string sandboxId, CancellationToken cancellationToken)
    {
        SandboxRecord record = await _sandboxRepository
            .GetAsync(sandboxId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return false;
        }

        await _sandboxRepository.DeleteAsync(record, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<SandboxInfoDto> GetSandboxAsync(string sandboxId, CancellationToken cancellationToken)
    {
        SandboxRecord record = await _sandboxRepository
            .GetAsync(sandboxId, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : MapSandbox(record);
    }

    public async Task<bool> PauseSandboxAsync(string sandboxId, CancellationToken cancellationToken)
    {
        SandboxRecord record = await _sandboxRepository
            .GetAsync(sandboxId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return false;
        }

        if (record.Status != SandboxStatus.RUNNING)
        {
            _logger.LogWarning(
                "Cannot pause sandbox {SandboxId} because it is in status {Status}",
                sandboxId,
                record.Status);
            return false;
        }

        bool paused = await _sandboxOrchestrationClient
            .PauseSandboxAsync(record.Id, record.RuntimeId, cancellationToken)
            .ConfigureAwait(false);

        if (!paused)
        {
            return false;
        }

        record.Status = SandboxStatus.PAUSED;
        record.SessionApiKey = null;
        record.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        await _sandboxRepository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> ResumeSandboxAsync(string sandboxId, CancellationToken cancellationToken)
    {
        SandboxRecord record = await _sandboxRepository
            .GetAsync(sandboxId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return false;
        }

        if (record.Status != SandboxStatus.PAUSED)
        {
            _logger.LogWarning(
                "Cannot resume sandbox {SandboxId} because it is in status {Status}",
                sandboxId,
                record.Status);
            return false;
        }

        SandboxProvisioningResult result = await _sandboxOrchestrationClient
            .ResumeSandboxAsync(record.Id, record.RuntimeId, cancellationToken)
            .ConfigureAwait(false);

        record.Status = result.Status;
        record.SessionApiKey = result.SessionApiKey;
        record.RuntimeId = result.RuntimeId ?? record.RuntimeId;
        record.RuntimeUrl = result.RuntimeUrl ?? record.RuntimeUrl;
        record.WorkspacePath = result.WorkspacePath ?? record.WorkspacePath;
        record.ExposedUrlsJson = SerializeExposedUrls(result.ExposedUrls);
        record.RuntimeHostsJson = SerializeRuntimeHosts(result.RuntimeHosts);
        record.RuntimeStateJson = result.RuntimeStateJson;
        record.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        await _sandboxRepository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<SandboxInfoDto> StartSandboxAsync(
        StartSandboxRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        SandboxSpecInfoDto spec = await ResolveSpecAsync(request.SandboxSpecId, cancellationToken)
            .ConfigureAwait(false);

        string sandboxId = Guid.NewGuid().ToString("N");
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        SandboxProvisioningResult result = await _sandboxOrchestrationClient
            .StartSandboxAsync(sandboxId, spec, cancellationToken)
            .ConfigureAwait(false);

        SandboxRecord record = new()
        {
            Id = sandboxId,
            SandboxSpecId = spec.Id,
            CreatedByUserId = request.CreatedByUserId,
            Status = result.Status,
            SessionApiKey = result.SessionApiKey,
            ExposedUrlsJson = SerializeExposedUrls(result.ExposedUrls),
            RuntimeId = result.RuntimeId,
            RuntimeUrl = result.RuntimeUrl,
            WorkspacePath = result.WorkspacePath,
            RuntimeHostsJson = SerializeRuntimeHosts(result.RuntimeHosts),
            RuntimeStateJson = result.RuntimeStateJson,
            CreatedAtUtc = timestamp,
            LastUpdatedAtUtc = timestamp,
        };

        await _sandboxRepository.AddAsync(record, cancellationToken).ConfigureAwait(false);
        return MapSandbox(record);
    }

    public async Task<SandboxPageDto> SearchSandboxesAsync(
        string pageId,
        int limit,
        CancellationToken cancellationToken)
    {
        int effectiveLimit = limit <= 0 ? DefaultLimit : Math.Min(limit, DefaultLimit);
        (IReadOnlyList<SandboxRecord> Items, string NextPageId) result = await _sandboxRepository
            .SearchAsync(pageId, effectiveLimit, cancellationToken)
            .ConfigureAwait(false);

        List<SandboxInfoDto> sandboxes = result.Items
            .Select(MapSandbox)
            .ToList();

        return new SandboxPageDto
        {
            Items = sandboxes,
            NextPageId = result.NextPageId,
        };
    }

    private static SandboxInfoDto MapSandbox(SandboxRecord record)
    {
        IReadOnlyList<ExposedUrlDto> exposedUrls = DeserializeList<ExposedUrlDto>(record.ExposedUrlsJson);
        IReadOnlyList<SandboxRuntimeHostDto> runtimeHosts = DeserializeList<SandboxRuntimeHostDto>(record.RuntimeHostsJson);

        return new SandboxInfoDto
        {
            Id = record.Id,
            SandboxSpecId = record.SandboxSpecId,
            CreatedByUserId = record.CreatedByUserId,
            Status = record.Status,
            SessionApiKey = record.SessionApiKey,
            ExposedUrls = exposedUrls,
            RuntimeId = record.RuntimeId,
            RuntimeUrl = record.RuntimeUrl,
            WorkspacePath = record.WorkspacePath,
            RuntimeHosts = runtimeHosts,
            CreatedAt = record.CreatedAtUtc,
        };
    }

    private static IReadOnlyList<T> DeserializeList<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<T>();
        }

        try
        {
            List<T> deserialized = JsonSerializer.Deserialize<List<T>>(json, SerializerOptions);
            return deserialized is null ? Array.Empty<T>() : deserialized;
        }
        catch (JsonException)
        {
            return Array.Empty<T>();
        }
    }

    private static string SerializeExposedUrls(IReadOnlyList<ExposedUrlDto> urls)
        => JsonSerializer.Serialize(urls ?? Array.Empty<ExposedUrlDto>(), SerializerOptions);

    private static string SerializeRuntimeHosts(IReadOnlyList<SandboxRuntimeHostDto> hosts)
        => JsonSerializer.Serialize(hosts ?? Array.Empty<SandboxRuntimeHostDto>(), SerializerOptions);

    private async Task<SandboxSpecInfoDto> ResolveSpecAsync(string requestedSpecId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedSpecId))
        {
            SandboxSpecInfoDto spec = await _sandboxSpecService
                .GetSandboxSpecAsync(requestedSpecId, cancellationToken)
                .ConfigureAwait(false);

            if (spec is null)
            {
                throw new InvalidOperationException($"Sandbox spec '{requestedSpecId}' was not found.");
            }

            return spec;
        }

        return await _sandboxSpecService.GetDefaultSandboxSpecAsync(cancellationToken).ConfigureAwait(false);
    }
}
