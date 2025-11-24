using System.IO;

namespace NetAI.Api.Models.Conversations;

public enum FileSelectionStatus
{
    Success,
    Binary,
    Error
}

public sealed class FileSelectionResultDto
{
    public FileSelectionStatus Status { get; init; }

    public string Code { get; init; }

    public string Error { get; init; }
}

public sealed class FileContentResponseDto
{
    public string Code { get; init; } = string.Empty;
}

public sealed class WorkspaceZipStreamDto
{
    public required Stream Content { get; init; }

    public string FileName { get; init; } = "workspace.zip";

    public string ContentType { get; init; } = "application/zip";
}
