using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetAI.RuntimeServer.Models;

namespace NetAI.RuntimeServer.Services;

public interface IWorkspaceService
{
    string RootPath { get; }

    string EnsureProjectWorkspace(string projectId);

    Task<IReadOnlyList<string>> ListFilesAsync(string? relativePath, string workspaceRootPath, CancellationToken cancellationToken);

    Task<WorkspaceFileSelection> ReadFileAsync(string relativePath, string workspaceRootPath, CancellationToken cancellationToken);

    Task<WorkspaceUploadResult> UploadFilesAsync(IReadOnlyList<RuntimeUploadedFile> files, string workspaceRootPath, CancellationToken cancellationToken);

    Task<RuntimeZipStreamResult> ZipWorkspaceAsync(string workspaceRootPath, CancellationToken cancellationToken);
}

public class WorkspaceFileSelection
{
    public string? Content { get; init; }

    public bool IsBinary { get; init; }

    public string? Error { get; init; }
}

public class WorkspaceUploadResult
{
    public IReadOnlyList<string> UploadedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RuntimeUploadSkippedFile> SkippedFiles { get; init; }
        = Array.Empty<RuntimeUploadSkippedFile>();
}

public class WorkspaceOptions
{
    public string RootPath { get; set; } = string.Empty;
}

public class FileSystemWorkspaceService : IWorkspaceService
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    private readonly string _rootPath;
    private readonly string _projectsRootPath;
    private readonly ILogger<FileSystemWorkspaceService> _logger;
    private readonly IDirectoryZipper _zipper;

    public FileSystemWorkspaceService(
        IOptions<WorkspaceOptions> options,
        IDirectoryZipper zipper,
        ILogger<FileSystemWorkspaceService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        WorkspaceOptions value = options.Value;
        if (string.IsNullOrWhiteSpace(value.RootPath))
        {
            throw new ArgumentException("Workspace root path must be provided", nameof(options));
        }

        string fullPath = Path.GetFullPath(value.RootPath);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        _rootPath = EnsureTrailingSeparator(fullPath);
        _projectsRootPath = EnsureTrailingSeparator(Path.Combine(_rootPath, "projects"));
        if (!Directory.Exists(_projectsRootPath))
        {
            Directory.CreateDirectory(_projectsRootPath);
        }
        Console.WriteLine($"[WorkspaceService] RootPath={_rootPath}; ProjectsRoot={_projectsRootPath}");
        _logger = logger;
        _zipper = zipper;
    }

    public string RootPath => _rootPath;

    public string EnsureProjectWorkspace(string projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        string projectPath = EnsureTrailingSeparator(Path.Combine(_projectsRootPath, projectId));
        if (!Directory.Exists(projectPath))
        {
            Directory.CreateDirectory(projectPath);
        }

        Console.WriteLine($"[WorkspaceService] Ensured project workspace at {projectPath}");

        return projectPath;
    }

    public Task<IReadOnlyList<string>> ListFilesAsync(string? relativePath, string workspaceRootPath, CancellationToken cancellationToken)
    {
        string targetPath;
        try
        {
            targetPath = ResolvePath(workspaceRootPath, relativePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve path {Path} for listing", relativePath);
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        if (File.Exists(targetPath))
        {
            string relative = NormalizeRelativePath(workspaceRootPath, targetPath);
            return Task.FromResult<IReadOnlyList<string>>(new[] { relative });
        }

        if (!Directory.Exists(targetPath))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var results = new List<string>();
        foreach (string file in Directory.EnumerateFiles(targetPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(NormalizeRelativePath(workspaceRootPath, file));
        }

        results.Sort(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyList<string>>(results);
    }

    public async Task<WorkspaceFileSelection> ReadFileAsync(string relativePath, string workspaceRootPath, CancellationToken cancellationToken)
    {
        string targetPath;
        try
        {
            targetPath = ResolvePath(workspaceRootPath, relativePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve file {Path}", relativePath);
            return new WorkspaceFileSelection
            {
                Error = "File is outside of the workspace."
            };
        }

        if (!File.Exists(targetPath))
        {
            return new WorkspaceFileSelection
            {
                Error = "File not found."
            };
        }

        await using FileStream stream = new(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (IsBinaryStream(stream))
        {
            return new WorkspaceFileSelection
            {
                IsBinary = true,
                Error = "Binary files cannot be previewed."
            };
        }

        stream.Position = 0;
        using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: true);
        string content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        return new WorkspaceFileSelection
        {
            Content = content
        };
    }

    public async Task<WorkspaceUploadResult> UploadFilesAsync(
        IReadOnlyList<RuntimeUploadedFile> files,
        string workspaceRootPath,
        CancellationToken cancellationToken)
    {
        var uploaded = new ConcurrentBag<string>();
        var skipped = new ConcurrentBag<RuntimeUploadSkippedFile>();

        foreach (RuntimeUploadedFile file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(file.FileName))
            {
                skipped.Add(new RuntimeUploadSkippedFile
                {
                    Name = file.FileName,
                    Reason = "File name is required."
                });
                continue;
            }

            string destinationPath;
            try
            {
                destinationPath = ResolvePath(workspaceRootPath, file.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping upload for {File}", file.FileName);
                skipped.Add(new RuntimeUploadSkippedFile
                {
                    Name = file.FileName,
                    Reason = "File is outside of the workspace."
                });
                continue;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await using FileStream targetStream = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                if (file.Content.CanSeek)
                {
                    file.Content.Position = 0;
                }
                await file.Content.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
                uploaded.Add(NormalizeRelativePath(workspaceRootPath, destinationPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file {File}", file.FileName);
                skipped.Add(new RuntimeUploadSkippedFile
                {
                    Name = file.FileName,
                    Reason = "Failed to write file."
                });
            }
        }

        return new WorkspaceUploadResult
        {
            UploadedFiles = uploaded.ToArray(),
            SkippedFiles = skipped.ToArray()
        };
    }

    public Task<RuntimeZipStreamResult> ZipWorkspaceAsync(string workspaceRootPath, CancellationToken cancellationToken)
    {
        return _zipper.ZipDirectoryAsync(workspaceRootPath, cancellationToken);
    }

    private string ResolvePath(string workspaceRootPath, string? relativePath)
    {
        string combined = string.IsNullOrWhiteSpace(relativePath)
            ? workspaceRootPath
            : Path.GetFullPath(Path.Combine(workspaceRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!combined.StartsWith(workspaceRootPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Path is outside of the workspace.");
        }

        return combined;
    }

    private string NormalizeRelativePath(string workspaceRootPath, string fullPath)
    {
        string relative = Path.GetRelativePath(workspaceRootPath, fullPath);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool IsBinaryStream(Stream stream)
    {
        const int ProbeLength = 1024;

        long originalPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            Span<byte> buffer = stackalloc byte[ProbeLength];
            int bytesRead = stream.Read(buffer);

            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        path = Path.GetFullPath(path);
        if (!path.EndsWith(Path.DirectorySeparatorChar))
        {
            path += Path.DirectorySeparatorChar;
        }

        return path;
    }
}
