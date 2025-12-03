using System.Text;
using NetAI.RuntimeServer.Models;

namespace NetAI.RuntimeServer.Services;

public interface IFileEditService
{
    Task<RuntimeFileEditResponseDto> ExecuteAsync(
        RuntimeFileEditRequestDto request,
        string workspaceRootPath,
        bool lintEnabled,
        CancellationToken cancellationToken);
}

public class FileEditService : IFileEditService
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    private readonly ILogger<FileEditService> _logger;

    public FileEditService(ILogger<FileEditService> logger)
    {
        _logger = logger;
    }

    public async Task<RuntimeFileEditResponseDto> ExecuteAsync(
        RuntimeFileEditRequestDto request,
        string workspaceRootPath,
        bool lintEnabled,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Error("invalid_request", "Request payload is required.");
        }

        string? operation = request.Operation?.Trim();
        if (string.IsNullOrWhiteSpace(operation))
        {
            return Error("invalid_operation", "Operation is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return Error("invalid_path", "Path is required.");
        }

        string targetPath;
        try
        {
            targetPath = ResolvePath(workspaceRootPath, request.Path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "File edit rejected: {Path} outside workspace", request.Path);
            return Error("invalid_path", "File is outside of the workspace.");
        }

        string normalizedOperation = operation.ToLowerInvariant();
        switch (normalizedOperation)
        {
            case "toggle_lint":
                bool newLint = request.LintEnabled ?? !lintEnabled;
                return new RuntimeFileEditResponseDto
                {
                    Path = NormalizeRelativePath(workspaceRootPath, targetPath),
                    LintEnabled = newLint
                };
            case "view":
                return await ViewAsync(request, targetPath, workspaceRootPath, cancellationToken).ConfigureAwait(false);
            case "insert":
                return await InsertAsync(request, targetPath, workspaceRootPath, cancellationToken).ConfigureAwait(false);
            case "replace":
                return await ReplaceAsync(request, targetPath, workspaceRootPath, cancellationToken).ConfigureAwait(false);
            case "diff":
                return await DiffAsync(request, targetPath, workspaceRootPath, cancellationToken).ConfigureAwait(false);
            default:
                return Error("invalid_operation", $"Unsupported operation: {operation}");
        }
    }

    private async Task<RuntimeFileEditResponseDto> ViewAsync(
        RuntimeFileEditRequestDto request,
        string fullPath,
        string workspaceRootPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(fullPath))
        {
            return Error("not_found", "File not found.");
        }

        await using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (IsBinaryStream(stream))
        {
            return Error("binary_file", "Binary files cannot be viewed.");
        }

        stream.Position = 0;
        using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: true);
        string content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        (int? start, int? end, string snippet, string? error) = ExtractRange(content, request.StartLine, request.EndLine);
        if (error is not null)
        {
            return Error("invalid_range", error);
        }

        return new RuntimeFileEditResponseDto
        {
            Path = NormalizeRelativePath(workspaceRootPath, fullPath),
            Content = snippet,
            StartLine = start,
            EndLine = end
        };
    }

    private async Task<RuntimeFileEditResponseDto> InsertAsync(
        RuntimeFileEditRequestDto request,
        string fullPath,
        string workspaceRootPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(fullPath))
        {
            return Error("not_found", "File not found.");
        }

        string original = await File.ReadAllTextAsync(fullPath, Utf8, cancellationToken).ConfigureAwait(false);
        string newline = DetectNewline(original);
        string[] lines = SplitLines(original);
        int targetLine = request.StartLine ?? lines.Length + 1;

        if (targetLine < 1 || targetLine > lines.Length + 1)
        {
            return Error("invalid_range", "Start line must be between 1 and the end of the file.");
        }

        string[] insertion = SplitLines(request.Content ?? string.Empty);
        var updatedLines = new List<string>(lines);
        updatedLines.InsertRange(targetLine - 1, insertion);

        string updatedContent = string.Join(newline, updatedLines);
        await File.WriteAllTextAsync(fullPath, updatedContent, Utf8, cancellationToken).ConfigureAwait(false);

        string diff = BuildUnifiedDiff(workspaceRootPath, fullPath, original, updatedContent, newline);

        return new RuntimeFileEditResponseDto
        {
            Path = NormalizeRelativePath(workspaceRootPath, fullPath),
            Content = updatedContent,
            StartLine = targetLine,
            EndLine = targetLine + insertion.Length - 1,
            Diff = diff
        };
    }

    private async Task<RuntimeFileEditResponseDto> ReplaceAsync(
        RuntimeFileEditRequestDto request,
        string fullPath,
        string workspaceRootPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(fullPath))
        {
            return Error("not_found", "File not found.");
        }

        string original = await File.ReadAllTextAsync(fullPath, Utf8, cancellationToken).ConfigureAwait(false);
        string newline = DetectNewline(original);
        string[] lines = SplitLines(original);

        if (!request.StartLine.HasValue || !request.EndLine.HasValue)
        {
            return Error("invalid_range", "Start and end lines are required for replace operations.");
        }

        int start = request.StartLine.Value;
        int end = request.EndLine.Value;

        if (start < 1 || end < start || end > lines.Length)
        {
            return Error("invalid_range", "Line range is outside of the file.");
        }

        string[] replacement = SplitLines(request.Content ?? string.Empty);
        var updatedLines = new List<string>(lines);
        updatedLines.RemoveRange(start - 1, end - start + 1);
        updatedLines.InsertRange(start - 1, replacement);

        string updatedContent = string.Join(newline, updatedLines);
        await File.WriteAllTextAsync(fullPath, updatedContent, Utf8, cancellationToken).ConfigureAwait(false);

        string diff = BuildUnifiedDiff(workspaceRootPath, fullPath, original, updatedContent, newline);

        return new RuntimeFileEditResponseDto
        {
            Path = NormalizeRelativePath(workspaceRootPath, fullPath),
            Content = updatedContent,
            StartLine = start,
            EndLine = start + replacement.Length - 1,
            Diff = diff
        };
    }

    private async Task<RuntimeFileEditResponseDto> DiffAsync(
        RuntimeFileEditRequestDto request,
        string fullPath,
        string workspaceRootPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(fullPath))
        {
            return Error("not_found", "File not found.");
        }

        string current = await File.ReadAllTextAsync(fullPath, Utf8, cancellationToken).ConfigureAwait(false);
        string baseline = request.Content ?? string.Empty;
        string newline = DetectNewline(current);

        string diff = BuildUnifiedDiff(workspaceRootPath, fullPath, baseline, current, newline);

        return new RuntimeFileEditResponseDto
        {
            Path = NormalizeRelativePath(workspaceRootPath, fullPath),
            Diff = diff
        };
    }

    private static string DetectNewline(string content)
    {
        if (content.Contains("\r\n"))
        {
            return "\r\n";
        }

        if (content.Contains('\r'))
        {
            return "\r";
        }

        return "\n";
    }

    private static string[] SplitLines(string content)
    {
        return content.Split('\n');
    }

    private static (int? Start, int? End, string Content, string? Error) ExtractRange(
        string content,
        int? startLine,
        int? endLine)
    {
        string[] lines = SplitLines(content);
        if (!startLine.HasValue && !endLine.HasValue)
        {
            return (null, null, content, null);
        }

        int start = startLine ?? 1;
        int end = endLine ?? lines.Length;

        if (start < 1 || end < start || end > lines.Length)
        {
            return (start, end, string.Empty, "Line range is outside of the file.");
        }

        IEnumerable<string> range = lines.Skip(start - 1).Take(end - start + 1);
        string snippet = string.Join('\n', range);
        return (start, end, snippet, null);
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

    private static string NormalizeRelativePath(string workspaceRootPath, string fullPath)
    {
        string relative = Path.GetRelativePath(workspaceRootPath, fullPath);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string ResolvePath(string workspaceRootPath, string relativePath)
    {
        string combined = Path.GetFullPath(Path.Combine(workspaceRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!combined.StartsWith(workspaceRootPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Path is outside of the workspace.");
        }

        return combined;
    }

    private static string BuildUnifiedDiff(
        string workspaceRootPath,
        string fullPath,
        string original,
        string updated,
        string newline)
    {
        string relativePath = NormalizeRelativePath(workspaceRootPath, fullPath);
        string header = $"--- a/{relativePath}{newline}+++ b/{relativePath}{newline}";

        string[] oldLines = SplitLines(original);
        string[] newLines = SplitLines(updated);

        var diffLines = new List<string>();
        foreach (var hunk in CalculateHunks(oldLines, newLines))
        {
            diffLines.Add($"@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@");
            diffLines.AddRange(hunk.Lines);
        }

        return header + string.Join(newline, diffLines);
    }

    private static IEnumerable<DiffHunk> CalculateHunks(string[] oldLines, string[] newLines)
    {
        int m = oldLines.Length;
        int n = newLines.Length;
        int[,] lcs = new int[m + 1, n + 1];

        for (int i = m - 1; i >= 0; i--)
        {
            for (int j = n - 1; j >= 0; j--)
            {
                if (oldLines[i] == newLines[j])
                {
                    lcs[i, j] = lcs[i + 1, j + 1] + 1;
                }
                else
                {
                    lcs[i, j] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
                }
            }
        }

        int oldIndex = 0;
        int newIndex = 0;
        var lines = new List<string>();
        int oldStart = 1;
        int newStart = 1;

        while (oldIndex < m || newIndex < n)
        {
            if (oldIndex < m && newIndex < n && oldLines[oldIndex] == newLines[newIndex])
            {
                oldIndex++;
                newIndex++;
                continue;
            }

            oldStart = oldIndex + 1;
            newStart = newIndex + 1;
            lines.Clear();

            while (oldIndex < m || newIndex < n)
            {
                if (oldIndex < m && newIndex < n && oldLines[oldIndex] == newLines[newIndex])
                {
                    break;
                }

                if (newIndex < n && (oldIndex == m || lcs[oldIndex, newIndex + 1] >= lcs[oldIndex + 1, newIndex]))
                {
                    lines.Add($"+{newLines[newIndex]}");
                    newIndex++;
                }
                else if (oldIndex < m)
                {
                    lines.Add($"-{oldLines[oldIndex]}");
                    oldIndex++;
                }
            }

            int oldCount = oldIndex - oldStart + 1;
            int newCount = newIndex - newStart + 1;
            if (oldCount == 0)
            {
                oldCount = 0;
            }
            if (newCount == 0)
            {
                newCount = 0;
            }

            yield return new DiffHunk(oldStart, Math.Max(oldCount, 0), newStart, Math.Max(newCount, 0), new List<string>(lines));
        }
    }

    private static RuntimeFileEditResponseDto Error(string code, string message)
    {
        return new RuntimeFileEditResponseDto
        {
            ErrorCode = code,
            Error = message
        };
    }

    private sealed record DiffHunk(int OldStart, int OldCount, int NewStart, int NewCount, IReadOnlyList<string> Lines);
}
