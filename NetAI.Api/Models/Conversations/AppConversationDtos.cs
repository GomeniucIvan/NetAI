using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Conversations;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AppConversationSortOrder
{
    [EnumMember(Value = "CREATED_AT")]
    CreatedAt,

    [EnumMember(Value = "CREATED_AT_DESC")]
    CreatedAtDesc,

    [EnumMember(Value = "UPDATED_AT")]
    UpdatedAt,

    [EnumMember(Value = "UPDATED_AT_DESC")]
    UpdatedAtDesc,

    [EnumMember(Value = "TITLE")]
    Title,

    [EnumMember(Value = "TITLE_DESC")]
    TitleDesc,
}

public class AppConversationTokenUsageDto
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("cache_read_tokens")]
    public int? CacheReadTokens { get; set; }

    [JsonPropertyName("cache_write_tokens")]
    public int? CacheWriteTokens { get; set; }

    [JsonPropertyName("reasoning_tokens")]
    public int? ReasoningTokens { get; set; }

    [JsonPropertyName("context_window")]
    public int? ContextWindow { get; set; }

    [JsonPropertyName("per_turn_token")]
    public int? PerTurnToken { get; set; }
}

public class AppConversationMetricsDto
{
    [JsonPropertyName("accumulated_cost")]
    public double AccumulatedCost { get; set; }

    [JsonPropertyName("max_budget_per_task")]
    public double? MaxBudgetPerTask { get; set; }

    [JsonPropertyName("accumulated_token_usage")]
    public AppConversationTokenUsageDto AccumulatedTokenUsage { get; set; }
}

public class AppConversationDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("created_by_user_id")]
    public string CreatedByUserId { get; set; }

    [JsonPropertyName("sandbox_id")]
    public string SandboxId { get; set; }

    [JsonPropertyName("selected_repository")]
    public string SelectedRepository { get; set; }

    [JsonPropertyName("selected_branch")]
    public string SelectedBranch { get; set; }

    [JsonPropertyName("git_provider")]
    public string GitProvider { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("trigger")]
    public string Trigger { get; set; }

    [JsonPropertyName("pr_number")]
    public IReadOnlyList<int> PullRequestNumbers { get; set; }

    [JsonPropertyName("llm_model")]
    public string LlmModel { get; set; }

    [JsonPropertyName("metrics")]
    public AppConversationMetricsDto Metrics { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("sandbox_status")]
    public string SandboxStatus { get; set; } = "MISSING";

    [JsonPropertyName("agent_status")]
    public string AgentStatus { get; set; }

    [JsonPropertyName("conversation_url")]
    public string ConversationUrl { get; set; }

    [JsonPropertyName("session_api_key")]
    public string SessionApiKey { get; set; }

    [JsonPropertyName("sandbox_vscode_url")]
    public string SandboxVscodeUrl { get; set; }
}

public class AppConversationPageDto
{
    [JsonPropertyName("items")]
    public IReadOnlyList<AppConversationDto> Items { get; set; } = Array.Empty<AppConversationDto>();

    [JsonPropertyName("next_page_id")]
    public string NextPageId { get; set; }
}
