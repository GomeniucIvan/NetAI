using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Configuration;

public class GetConfigResponseDto
{
    [JsonPropertyName("APP_MODE")]
    public AppMode AppMode { get; init; }

    [JsonPropertyName("APP_SLUG")]
    public string AppSlug { get; init; }

    [JsonPropertyName("GITHUB_CLIENT_ID")]
    public string GithubClientId { get; init; }

    [JsonPropertyName("POSTHOG_CLIENT_KEY")]
    public string PosthogClientKey { get; init; }

    [JsonPropertyName("PROVIDERS_CONFIGURED")]
    public IReadOnlyList<ProviderOption> ProvidersConfigured { get; init; }

    [JsonPropertyName("AUTH_URL")]
    public string AuthUrl { get; init; }

    [JsonPropertyName("FEATURE_FLAGS")]
    public FeatureFlagsDto FeatureFlags { get; init; } = new();

    [JsonPropertyName("MAINTENANCE")]
    public MaintenanceWindowDto Maintenance { get; init; }
}

public class FeatureFlagsDto
{
    [JsonPropertyName("ENABLE_BILLING")]
    public bool EnableBilling { get; init; }

    [JsonPropertyName("HIDE_LLM_SETTINGS")]
    public bool HideLlmSettings { get; init; }

    [JsonPropertyName("ENABLE_JIRA")]
    public bool EnableJira { get; init; }

    [JsonPropertyName("ENABLE_JIRA_DC")]
    public bool EnableJiraDc { get; init; }

    [JsonPropertyName("ENABLE_LINEAR")]
    public bool EnableLinear { get; init; }
}

public class MaintenanceWindowDto
{
    [JsonPropertyName("startTime")]
    public string StartTime { get; init; }
}
