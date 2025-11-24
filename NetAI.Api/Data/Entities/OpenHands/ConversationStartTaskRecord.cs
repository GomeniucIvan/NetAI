using System.ComponentModel.DataAnnotations;

namespace NetAI.Api.Data.Entities.OpenHands;

public class ConversationStartTaskRecord
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string CreatedByUserId { get; set; }

    public ConversationStartTaskStatus Status { get; set; } = ConversationStartTaskStatus.Working;

    [MaxLength(512)]
    public string Detail { get; set; }

    [MaxLength(512)]
    public string FailureDetail { get; set; }

    [MaxLength(100)]
    public string AppConversationId { get; set; }

    [MaxLength(100)]
    public string SandboxId { get; set; }

    [MaxLength(512)]
    public string AgentServerUrl { get; set; }

    [MaxLength(200)]
    public string SandboxSessionApiKey { get; set; }

    [MaxLength(512)]
    public string SandboxWorkspacePath { get; set; }

    [MaxLength(512)]
    public string SandboxVscodeUrl { get; set; }

    [MaxLength(100)]
    public string ConversationStatus { get; set; }

    [MaxLength(100)]
    public string RuntimeStatus { get; set; }

    [MaxLength(512)]
    public string BackendError { get; set; }

    public string RequestJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
