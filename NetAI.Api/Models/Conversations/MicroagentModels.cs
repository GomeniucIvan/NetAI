using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Conversations;

public class InputMetadataDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }
}

public class MicroagentDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("triggers")]
    public IReadOnlyList<string> Triggers { get; set; } = Array.Empty<string>();

    [JsonPropertyName("inputs")]
    public IReadOnlyList<InputMetadataDto> Inputs { get; set; } = Array.Empty<InputMetadataDto>();

    [JsonPropertyName("tools")]
    public IReadOnlyList<string> Tools { get; set; } = Array.Empty<string>();
}

public class GetMicroagentsResponseDto
{
    [JsonPropertyName("microagents")]
    public IReadOnlyList<MicroagentDto> Microagents { get; set; } = Array.Empty<MicroagentDto>();
}

public class GetMicroagentPromptResponseDto
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "ok";

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }
}
