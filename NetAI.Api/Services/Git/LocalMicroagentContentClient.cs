using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NetAI.Api.Services.Git;

public class LocalMicroagentContentClient : IMicroagentContentClient
{
    private const string ProviderName = "local";
    private const string MicroagentRootRelativePath = ".openhands/microagents";

    private readonly ILogger<LocalMicroagentContentClient> _logger;
    private readonly string _contentRootPath;
    private readonly string _microagentRootPath;

    public LocalMicroagentContentClient(IHostEnvironment hostEnvironment, ILogger<LocalMicroagentContentClient> logger)
    {
        _logger = logger;
        _contentRootPath = Path.GetFullPath(hostEnvironment.ContentRootPath);
        _microagentRootPath = Path.GetFullPath(Path.Combine(_contentRootPath, MicroagentRootRelativePath));
    }

    public Task<IReadOnlyList<MicroagentFileDescriptor>> GetMicroagentFilesAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(_microagentRootPath))
        {
            _logger.LogWarning(
                "Microagent directory {MicroagentRootPath} was not found. No embedded microagents are available.",
                _microagentRootPath);
            return Task.FromResult<IReadOnlyList<MicroagentFileDescriptor>>(Array.Empty<MicroagentFileDescriptor>());
        }

        IEnumerable<MicroagentFileDescriptor> descriptors = Directory
            .EnumerateFiles(_microagentRootPath, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(filePath => CreateDescriptor(filePath));

        return Task.FromResult<IReadOnlyList<MicroagentFileDescriptor>>(descriptors.ToImmutableArray());
    }

    public Task<MicroagentFileContent> GetMicroagentFileContentAsync(
        string owner,
        string repository,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new GitResourceNotFoundException("Microagent path must be provided.");
        }

        string sanitized = path.TrimStart('/', '\\');
        string combinedPath = Path.Combine(_contentRootPath, sanitized.Replace('/', Path.DirectorySeparatorChar));
        string fullPath = Path.GetFullPath(combinedPath);

        if (!fullPath.StartsWith(_microagentRootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new GitResourceNotFoundException($"Microagent '{path}' was not found.");
        }

        if (!File.Exists(fullPath))
        {
            throw new GitResourceNotFoundException($"Microagent '{path}' was not found.");
        }

        string content = File.ReadAllText(fullPath);
        DateTimeOffset? lastModifiedAt = File.GetLastWriteTimeUtc(fullPath);

        return Task.FromResult(new MicroagentFileContent(path, content, ProviderName, lastModifiedAt));
    }

    private MicroagentFileDescriptor CreateDescriptor(string filePath)
    {
        string relativePath = Path.GetRelativePath(_contentRootPath, filePath);
        string normalizedPath = NormalizePath(relativePath);
        string fileName = Path.GetFileName(filePath);
        DateTimeOffset? createdAt = File.GetLastWriteTimeUtc(filePath);

        return new MicroagentFileDescriptor(fileName, normalizedPath, createdAt, ProviderName);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/');
    }
}
