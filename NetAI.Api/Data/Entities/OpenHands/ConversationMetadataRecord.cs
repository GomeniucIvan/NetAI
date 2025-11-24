using System.ComponentModel.DataAnnotations;
using NetAI.Api.Data.Entities.Conversations;

namespace NetAI.Api.Data.Entities.OpenHands;

public class ConversationMetadataRecord
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public string ConversationId { get; set; } 

    [MaxLength(512)]
    public string SelectedRepository { get; set; }

    [MaxLength(200)]
    public string UserId { get; set; }

    [MaxLength(200)]
    public string SelectedBranch { get; set; }

    public ProviderType? GitProvider { get; set; }

    [MaxLength(50)]
    public string GitProviderRaw { get; set; }

    [MaxLength(512)]
    public string Title { get; set; }

    public DateTime? LastUpdatedAtUtc { get; set; }

    public ConversationTrigger? Trigger { get; set; }

    public List<int> PullRequestNumbers { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(150)]
    public string LlmModel { get; set; }

    public double AccumulatedCost { get; set; }

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    [MaxLength(100)]
    public string SandboxId { get; set; }

    [MaxLength(50)]
    public string ConversationVersion { get; set; }

    public ConversationStatus Status { get; set; } = ConversationStatus.Starting;

    [MaxLength(100)]
    public string RuntimeStatus { get; set; }

    [MaxLength(200)]
    public string RuntimeId { get; set; }

    [MaxLength(200)]
    public string SessionId { get; set; }

    [MaxLength(200)]
    public string SessionApiKey { get; set; }

    [MaxLength(512)]
    public string Url { get; set; }

    [MaxLength(512)]
    public string VscodeUrl { get; set; }

    public ICollection<ConversationEventRecord> Events { get; set; } = new List<ConversationEventRecord>();

    public ConversationRuntimeInstanceRecord RuntimeInstance { get; set; }

    public ICollection<ConversationMicroagentRecord> Microagents { get; set; } = new List<ConversationMicroagentRecord>();

    public ICollection<ConversationFileRecord> Files { get; set; } = new List<ConversationFileRecord>();

    public ICollection<ConversationGitDiffRecord> GitDiffs { get; set; } = new List<ConversationGitDiffRecord>();

    public ICollection<ConversationFeedbackRecord> FeedbackEntries { get; set; } = new List<ConversationFeedbackRecord>();

    public ICollection<ConversationRememberPromptRecord> RememberPrompts { get; set; } = new List<ConversationRememberPromptRecord>();
}
