namespace NetAI.Api.Services.Git;

public interface IMicroagentContentClient
{
    Task<IReadOnlyList<MicroagentFileDescriptor>> GetMicroagentFilesAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken);

    Task<MicroagentFileContent> GetMicroagentFileContentAsync(
        string owner,
        string repository,
        string path,
        CancellationToken cancellationToken);
}

public record MicroagentFileDescriptor(
    string Name,
    string Path,
    DateTimeOffset? CreatedAt,
    string GitProvider);

public record MicroagentFileContent(
    string Path,
    string Content,
    string GitProvider,
    DateTimeOffset? LastModifiedAt);
