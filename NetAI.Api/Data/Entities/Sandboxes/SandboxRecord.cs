using NetAI.Api.Models.Sandboxes;

namespace NetAI.Api.Data.Entities.Sandboxes;

public class SandboxRecord
{
    public string Id { get; set; }

    public string SandboxSpecId { get; set; }

    public string CreatedByUserId { get; set; }

    public SandboxStatus Status { get; set; } = SandboxStatus.STARTING;

    public string SessionApiKey { get; set; }

    public string ExposedUrlsJson { get; set; } = "[]";

    public string RuntimeId { get; set; }

    public string RuntimeUrl { get; set; }

    public string WorkspacePath { get; set; }

    public string RuntimeHostsJson { get; set; } = "[]";

    public string RuntimeStateJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastUpdatedAtUtc { get; set; }
}
