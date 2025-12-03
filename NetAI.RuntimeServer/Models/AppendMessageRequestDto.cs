using System.Text.Json.Serialization;

namespace NetAI.RuntimeServer.Models;

public record AppendMessageRequestDto(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("source")] string? Source);
