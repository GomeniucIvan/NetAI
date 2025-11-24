using System;

namespace NetAI.Api.Data.Entities.Sandboxes;

public class SandboxSpecRecord
{
    public string Id { get; set; }

    public string CommandJson { get; set; }

    public string InitialEnvJson { get; set; } = "{}";

    public string WorkingDir { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
