using Microsoft.Extensions.Options;
using NetAI.Server.Options;

namespace NetAI.Server.Services;

public sealed class WorkspaceDirectoryProvider
{
    public string WorkspacePath { get; }

    public WorkspaceDirectoryProvider(IOptions<WorkspaceOptions> options, ILogger<WorkspaceDirectoryProvider> logger)
    {
        var configuredPath = options.Value.RootPath;
        var workspacePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
            : Path.GetFullPath(configuredPath);

        Directory.CreateDirectory(workspacePath);

        if (!IsTempPath(workspacePath))
        {
            logger.LogWarning("Using configured workspace at '{WorkspacePath}'. Agent can EDIT files here.", workspacePath);
        }

        WorkspacePath = workspacePath;
    }

    private static bool IsTempPath(string path)
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return targetPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase);
    }
}
