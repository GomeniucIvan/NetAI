using System.ComponentModel.DataAnnotations;

namespace NetAI.SandboxOrchestration.Options;

public class OpenHandsOptions
{
    public const string SectionName = "OpenHands";

    public string Provider { get; set; } = "openhands";

    //[Required]
    [Url]
    public string ApiBaseUrl { get; set; }

    public string ApiKey { get; set; }

    [Range(1, int.MaxValue)]
    public int RequestTimeoutSeconds { get; set; } = 120;
}
