using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NetAI.Api.Data.Entities.Sandboxes;
using NetAI.Api.Data.Repositories;
using NetAI.Api.Models.Sandboxes;

namespace NetAI.Api.Services.Sandboxes;

public class SandboxSpecService : ISandboxSpecService
{
    private const int DefaultLimit = 100;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ISandboxSpecRepository _sandboxSpecRepository;
    private readonly DefaultSandboxSpecOptions _defaultSpecOptions;

    public SandboxSpecService(
        ISandboxSpecRepository sandboxSpecRepository,
        IOptions<DefaultSandboxSpecOptions> defaultSpecOptions = null)
    {
        _sandboxSpecRepository = sandboxSpecRepository;
        _defaultSpecOptions = defaultSpecOptions?.Value ?? new DefaultSandboxSpecOptions();
    }

    public async Task<IReadOnlyList<SandboxSpecInfoDto>> BatchGetSandboxSpecsAsync(
        IReadOnlyList<string> sandboxSpecIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, SandboxSpecRecord> lookup = await _sandboxSpecRepository
            .BatchGetAsync(sandboxSpecIds, cancellationToken)
            .ConfigureAwait(false);

        string defaultSpecId = ResolveDefaultSpecId();

        return sandboxSpecIds
            .Select(id =>
            {
                if (lookup.TryGetValue(id, out SandboxSpecRecord record))
                {
                    return Map(record);
                }

                if (string.Equals(id, defaultSpecId, StringComparison.OrdinalIgnoreCase))
                {
                    return CreateDefaultSpec();
                }

                return null;
            })
            .ToList();
    }

    public async Task<SandboxSpecInfoDto> GetDefaultSandboxSpecAsync(CancellationToken cancellationToken)
    {
        SandboxSpecInfoPageDto page = await SearchSandboxSpecsAsync(null, 1, cancellationToken)
            .ConfigureAwait(false);

        if (page.Items.Count == 0)
        {
            return CreateDefaultSpec();
        }

        return page.Items[0];
    }

    public async Task<SandboxSpecInfoDto> GetSandboxSpecAsync(
        string sandboxSpecId,
        CancellationToken cancellationToken)
    {
        SandboxSpecRecord record = await _sandboxSpecRepository
            .GetAsync(sandboxSpecId, cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            return Map(record);
        }

        string defaultSpecId = ResolveDefaultSpecId();
        if (string.Equals(sandboxSpecId, defaultSpecId, StringComparison.OrdinalIgnoreCase))
        {
            return CreateDefaultSpec();
        }

        return null;
    }

    public async Task<SandboxSpecInfoPageDto> SearchSandboxSpecsAsync(
        string pageId,
        int limit,
        CancellationToken cancellationToken)
    {
        int effectiveLimit = limit <= 0 ? DefaultLimit : Math.Min(limit, DefaultLimit);
        (IReadOnlyList<SandboxSpecRecord> Items, string NextPageId) result = await _sandboxSpecRepository
            .SearchAsync(pageId, effectiveLimit, cancellationToken)
            .ConfigureAwait(false);

        List<SandboxSpecInfoDto> specs = result.Items
            .Select(Map)
            .ToList();

        if (specs.Count == 0)
        {
            specs.Add(CreateDefaultSpec());
        }

        return new SandboxSpecInfoPageDto
        {
            Items = specs,
            NextPageId = result.NextPageId,
        };
    }

    private SandboxSpecInfoDto CreateDefaultSpec()
    {
        string id = ResolveDefaultSpecId();
        IReadOnlyList<string> command = _defaultSpecOptions.Command?.Where(static c => !string.IsNullOrWhiteSpace(c)).ToList();
        if (command is { Count: 0 })
        {
            command = null;
        }

        IReadOnlyDictionary<string, string> env = _defaultSpecOptions.InitialEnv is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(_defaultSpecOptions.InitialEnv, StringComparer.OrdinalIgnoreCase);

        string workingDir = string.IsNullOrWhiteSpace(_defaultSpecOptions.WorkingDir)
            ? "/workspace"
            : _defaultSpecOptions.WorkingDir;

        return new SandboxSpecInfoDto
        {
            Id = id,
            Command = command,
            CreatedAt = DateTimeOffset.UtcNow,
            InitialEnv = env,
            WorkingDir = workingDir,
        };
    }

    private string ResolveDefaultSpecId()
    {
        return string.IsNullOrWhiteSpace(_defaultSpecOptions.Id)
            ? DefaultSandboxSpecOptions.DefaultImage
            : _defaultSpecOptions.Id;
    }

    private static SandboxSpecInfoDto Map(SandboxSpecRecord record)
    {
        IReadOnlyList<string> command = null;
        if (!string.IsNullOrWhiteSpace(record.CommandJson))
        {
            try
            {
                command = JsonSerializer.Deserialize<List<string>>(record.CommandJson, SerializerOptions);
            }
            catch (JsonException)
            {
                command = null;
            }
        }

        IReadOnlyDictionary<string, string> initialEnv = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(record.InitialEnvJson))
        {
            try
            {
                Dictionary<string, string> env = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    record.InitialEnvJson,
                    SerializerOptions);
                if (env is not null)
                {
                    initialEnv = env;
                }
            }
            catch (JsonException)
            {
                initialEnv = new Dictionary<string, string>();
            }
        }

        return new SandboxSpecInfoDto
        {
            Id = record.Id,
            Command = command,
            CreatedAt = record.CreatedAtUtc,
            InitialEnv = initialEnv,
            WorkingDir = record.WorkingDir,
        };
    }
}
