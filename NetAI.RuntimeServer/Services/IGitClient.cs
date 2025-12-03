using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetAI.RuntimeServer.Models;

namespace NetAI.RuntimeServer.Services;

public interface IGitClient
{
    Task<IReadOnlyList<RuntimeGitChangeResult>> GetChangesAsync(string workingDirectory, CancellationToken cancellationToken);

    Task<string?> GetFileContentAsync(string workingDirectory, string relativePath, CancellationToken cancellationToken);
}

public class GitClientOptions
{
    public string ExecutablePath { get; set; } = "git";

    public string StatusArguments { get; set; } = "status --porcelain";

    public string ShowFileFormat { get; set; } = "show HEAD:{0}";

    public int CommandTimeoutSeconds { get; set; } = 30;
}

public class GitClient : IGitClient
{
    private readonly GitClientOptions _options;
    private readonly ILogger<GitClient> _logger;

    public GitClient(IOptions<GitClientOptions> options, ILogger<GitClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentException("Git client options are required", nameof(options));
        _logger = logger;
    }

    public async Task<IReadOnlyList<RuntimeGitChangeResult>> GetChangesAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        CommandResult result = await RunGitAsync(_options.StatusArguments, workingDirectory, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                _logger.LogWarning("git status failed: {Error}", result.StandardError);
            }

            return Array.Empty<RuntimeGitChangeResult>();
        }

        var changes = new List<RuntimeGitChangeResult>();
        using var reader = new StringReader(result.StandardOutput);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length < 3)
            {
                continue;
            }

            string statusSegment = line[..2];
            string pathSegment = line.Length > 3 ? line[3..] : string.Empty;

            if (string.IsNullOrWhiteSpace(pathSegment))
            {
                continue;
            }

            string path = ParsePath(pathSegment);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string status = NormalizeStatus(statusSegment);

            changes.Add(new RuntimeGitChangeResult
            {
                Path = path,
                Status = status
            });
        }

        return changes;
    }

    public async Task<string?> GetFileContentAsync(
        string workingDirectory,
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        string normalized = relativePath.Replace(Path.DirectorySeparatorChar, '/');
        string arguments = string.Format(CultureInfo.InvariantCulture, _options.ShowFileFormat, normalized);

        CommandResult result = await RunGitAsync(arguments, workingDirectory, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            string error = string.IsNullOrWhiteSpace(result.StandardError)
                ? string.Empty
                : result.StandardError.Trim();
            _logger.LogDebug(
                "git show failed for {Path}: {Error}",
                relativePath,
                error);
            return null;
        }

        return result.StandardOutput;
    }

    private async Task<CommandResult> RunGitAsync(string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException("Working directory must be provided.", nameof(workingDirectory));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start git process");
            return CommandResult.Failure();
        }

        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync();

        Task waitForExitTask = process.WaitForExitAsync(cancellationToken);
        Task timeoutTask = Task.Delay(GetTimeout(), CancellationToken.None);

        Task completed = await Task.WhenAny(waitForExitTask, timeoutTask).ConfigureAwait(false);

        if (completed == timeoutTask)
        {
            TryTerminate(process);
            return CommandResult.Failure("Command timed out.");
        }

        try
        {
            await waitForExitTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryTerminate(process);
            throw;
        }

        string stdout = await stdOutTask.ConfigureAwait(false);
        string stderr = await stdErrTask.ConfigureAwait(false);

        bool success = process.ExitCode == 0;
        return new CommandResult(success, stdout, stderr);
    }

    private TimeSpan GetTimeout()
    {
        int seconds = Math.Clamp(_options.CommandTimeoutSeconds, 1, 300);
        return TimeSpan.FromSeconds(seconds);
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static string ParsePath(string segment)
    {
        string trimmed = segment.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return string.Empty;
        }

        int renameIndex = trimmed.IndexOf("->", StringComparison.Ordinal);
        if (renameIndex >= 0)
        {
            trimmed = trimmed[(renameIndex + 2)..].Trim();
        }

        return trimmed.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string NormalizeStatus(string statusSegment)
    {
        char first = statusSegment.Length > 0 ? statusSegment[0] : ' ';
        char second = statusSegment.Length > 1 ? statusSegment[1] : ' ';

        foreach (char value in new[] { first, second })
        {
            switch (value)
            {
                case 'A':
                    return "added";
                case 'M':
                    return "modified";
                case 'D':
                    return "deleted";
                case 'R':
                    return "renamed";
            }
        }

        if (first == '?' && second == '?')
        {
            return "untracked";
        }

        return statusSegment.Trim();
    }

    private readonly record struct CommandResult(bool Success, string StandardOutput, string StandardError)
    {
        public static CommandResult Failure(string? error = null)
            => new(false, string.Empty, error ?? string.Empty);
    }
}
