using System.ComponentModel.DataAnnotations;

namespace NetAI.Api.Data.Entities.OpenHands;

//todo ref

public class OpenHandsSettingsRecord
{
    public Guid Id { get; set; }

    [MaxLength(10)]
    public string Language { get; set; }

    [MaxLength(100)]
    public string Agent { get; set; }

    public int? MaxIterations { get; set; }

    [MaxLength(100)]
    public string SecurityAnalyzer { get; set; }

    public bool? ConfirmationMode { get; set; }

    [MaxLength(150)]
    public string LlmModel { get; set; }

    [MaxLength(512)]
    public string LlmApiKey { get; set; }

    [MaxLength(512)]
    public string LlmBaseUrl { get; set; }

    public int? RemoteRuntimeResourceFactor { get; set; }

    public bool EnableDefaultCondenser { get; set; } = true;

    public bool EnableSoundNotifications { get; set; }

    public bool EnableProactiveConversationStarters { get; set; } = true;

    public bool EnableSolvabilityAnalysis { get; set; } = true;

    public bool? UserConsentsToAnalytics { get; set; }

    [MaxLength(256)]
    public string SandboxBaseContainerImage { get; set; }

    [MaxLength(256)]
    public string SandboxRuntimeContainerImage { get; set; }

    public string McpConfigJson { get; set; }

    [MaxLength(512)]
    public string SearchApiKey { get; set; }

    [MaxLength(512)]
    public string SandboxApiKey { get; set; }

    public double? MaxBudgetPerTask { get; set; }

    public int? CondenserMaxSize { get; set; }

    [MaxLength(256)]
    public string Email { get; set; }

    public bool? EmailVerified { get; set; }

    [MaxLength(200)]
    public string GitUserName { get; set; }

    [MaxLength(256)]
    public string GitUserEmail { get; set; }

    [MaxLength(512)]
    public string AdditionalSettingsJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public UserSecretsRecord SecretsStore { get; set; } = null!;
}
