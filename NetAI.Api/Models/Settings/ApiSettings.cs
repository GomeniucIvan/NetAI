using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Settings;

public class ApiSettingsDto
{
    [JsonPropertyName("language")]
    public string Language { get; init; }

    [JsonPropertyName("agent")]
    public string Agent { get; init; }

    [JsonPropertyName("max_iterations")]
    public int? MaxIterations { get; init; }

    [JsonPropertyName("security_analyzer")]
    public string SecurityAnalyzer { get; init; }

    [JsonPropertyName("confirmation_mode")]
    public bool ConfirmationMode { get; init; }

    [JsonPropertyName("llm_model")]
    public string LlmModel { get; init; }

    [JsonPropertyName("llm_api_key")]
    public string LlmApiKey { get; init; }

    [JsonPropertyName("llm_base_url")]
    public string LlmBaseUrl { get; init; }

    [JsonPropertyName("remote_runtime_resource_factor")]
    public double? RemoteRuntimeResourceFactor { get; init; }

    [JsonPropertyName("enable_default_condenser")]
    public bool EnableDefaultCondenser { get; init; }

    [JsonPropertyName("condenser_max_size")]
    public int? CondenserMaxSize { get; init; }

    [JsonPropertyName("enable_sound_notifications")]
    public bool EnableSoundNotifications { get; init; }

    [JsonPropertyName("enable_proactive_conversation_starters")]
    public bool EnableProactiveConversationStarters { get; init; }

    [JsonPropertyName("enable_solvability_analysis")]
    public bool EnableSolvabilityAnalysis { get; init; }

    [JsonPropertyName("user_consents_to_analytics")]
    public bool? UserConsentsToAnalytics { get; init; }

    [JsonPropertyName("sandbox_base_container_image")]
    public string SandboxBaseContainerImage { get; init; }

    [JsonPropertyName("sandbox_runtime_container_image")]
    public string SandboxRuntimeContainerImage { get; init; }

    [JsonPropertyName("mcp_config")]
    public McpConfigDto McpConfig { get; init; }

    [JsonPropertyName("search_api_key")]
    public string SearchApiKey { get; init; }

    [JsonPropertyName("sandbox_api_key")]
    public string SandboxApiKey { get; init; }

    [JsonPropertyName("llm_api_key_set")]
    public bool LlmApiKeySet { get; init; }

    [JsonPropertyName("search_api_key_set")]
    public bool SearchApiKeySet { get; init; }

    [JsonPropertyName("provider_tokens_set")]
    public IDictionary<string, string> ProviderTokensSet { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("max_budget_per_task")]
    public double? MaxBudgetPerTask { get; init; }

    [JsonPropertyName("email")]
    public string Email { get; init; }

    [JsonPropertyName("email_verified")]
    public bool? EmailVerified { get; init; }

    [JsonPropertyName("git_user_name")]
    public string GitUserName { get; init; }

    [JsonPropertyName("git_user_email")]
    public string GitUserEmail { get; init; }

    [JsonPropertyName("is_new_user")]
    public bool? IsNewUser { get; init; }
}

public class UpdateSettingsRequestDto
{
    [JsonPropertyName("language")]
    public string Language { get; init; }

    [JsonPropertyName("agent")]
    public string Agent { get; init; }

    [JsonPropertyName("max_iterations")]
    public int? MaxIterations { get; init; }

    [JsonPropertyName("security_analyzer")]
    public string SecurityAnalyzer { get; init; }

    [JsonPropertyName("confirmation_mode")]
    public bool? ConfirmationMode { get; init; }

    [JsonPropertyName("llm_model")]
    public string LlmModel { get; init; }

    [JsonPropertyName("llm_api_key")]
    public string LlmApiKey { get; init; }

    [JsonPropertyName("llm_base_url")]
    public string LlmBaseUrl { get; init; }

    [JsonPropertyName("remote_runtime_resource_factor")]
    public double? RemoteRuntimeResourceFactor { get; init; }

    [JsonPropertyName("enable_default_condenser")]
    public bool? EnableDefaultCondenser { get; init; }

    [JsonPropertyName("condenser_max_size")]
    public int? CondenserMaxSize { get; init; }

    [JsonPropertyName("enable_sound_notifications")]
    public bool? EnableSoundNotifications { get; init; }

    [JsonPropertyName("enable_proactive_conversation_starters")]
    public bool? EnableProactiveConversationStarters { get; init; }

    [JsonPropertyName("enable_solvability_analysis")]
    public bool? EnableSolvabilityAnalysis { get; init; }

    [JsonPropertyName("user_consents_to_analytics")]
    public bool? UserConsentsToAnalytics { get; init; }

    [JsonPropertyName("sandbox_base_container_image")]
    public string SandboxBaseContainerImage { get; init; }

    [JsonPropertyName("sandbox_runtime_container_image")]
    public string SandboxRuntimeContainerImage { get; init; }

    [JsonPropertyName("mcp_config")]
    public McpConfigDto McpConfig { get; init; }

    [JsonPropertyName("search_api_key")]
    public string SearchApiKey { get; init; }

    [JsonPropertyName("sandbox_api_key")]
    public string SandboxApiKey { get; init; }

    [JsonPropertyName("max_budget_per_task")]
    public double? MaxBudgetPerTask { get; init; }

    [JsonPropertyName("email")]
    public string Email { get; init; }

    [JsonPropertyName("email_verified")]
    public bool? EmailVerified { get; init; }

    [JsonPropertyName("git_user_name")]
    public string GitUserName { get; init; }

    [JsonPropertyName("git_user_email")]
    public string GitUserEmail { get; init; }
}

public class McpConfigDto
{
    [JsonPropertyName("sse_servers")]
    public IList<McpServerReferenceDto> SseServers { get; init; } = new List<McpServerReferenceDto>();

    [JsonPropertyName("stdio_servers")]
    public IList<McpStdioServerDto> StdioServers { get; init; } = new List<McpStdioServerDto>();

    [JsonPropertyName("shttp_servers")]
    public IList<McpServerReferenceDto> ShttpServers { get; init; } = new List<McpServerReferenceDto>();

    public McpConfigDto Clone()
    {
        return new McpConfigDto
        {
            SseServers = SseServers.Select(server => server.Clone()).ToList(),
            StdioServers = StdioServers.Select(server => server.Clone()).ToList(),
            ShttpServers = ShttpServers.Select(server => server.Clone()).ToList()
        };
    }
}

[JsonConverter(typeof(McpServerReferenceDtoConverter))]
public class McpServerReferenceDto
{
    [JsonPropertyName("url")]
    public string Url { get; init; }

    [JsonPropertyName("api_key")]
    public string ApiKey { get; init; }

    [JsonPropertyName("timeout")]
    public double? Timeout { get; init; }

    public McpServerReferenceDto Clone()
        => new()
        {
            Url = Url,
            ApiKey = ApiKey,
            Timeout = Timeout
        };
}

public class McpStdioServerDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("command")]
    public string Command { get; init; }

    [JsonPropertyName("args")]
    public IList<string> Args { get; init; } = new List<string>();

    [JsonPropertyName("env")]
    public IDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();

    public McpStdioServerDto Clone()
        => new()
        {
            Name = Name,
            Command = Command,
            Args = Args.ToList(),
            Env = Env.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
}

public sealed class McpServerReferenceDtoConverter : JsonConverter<McpServerReferenceDto>
{
    public override McpServerReferenceDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string url = reader.GetString();
            return new McpServerReferenceDto { Url = url };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected string or object for MCP server definition");
        }

        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;

        string parsedUrl = root.TryGetProperty("url", out JsonElement urlElement) ? urlElement.GetString() : null;
        string parsedApiKey = root.TryGetProperty("api_key", out JsonElement apiKeyElement) ? apiKeyElement.GetString() : null;
        double? parsedTimeout = root.TryGetProperty("timeout", out JsonElement timeoutElement) && timeoutElement.ValueKind == JsonValueKind.Number
            ? timeoutElement.GetDouble()
            : null;

        return new McpServerReferenceDto
        {
            Url = parsedUrl,
            ApiKey = parsedApiKey,
            Timeout = parsedTimeout
        };
    }

    public override void Write(Utf8JsonWriter writer, McpServerReferenceDto value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        if (!string.IsNullOrEmpty(value.Url))
        {
            writer.WriteString("url", value.Url);
        }

        if (!string.IsNullOrEmpty(value.ApiKey))
        {
            writer.WriteString("api_key", value.ApiKey);
        }

        if (value.Timeout.HasValue)
        {
            writer.WriteNumber("timeout", value.Timeout.Value);
        }

        writer.WriteEndObject();
    }
}
