using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Keys;

public record class ApiKeyDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("prefix")]
    public string Prefix { get; init; } 

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
        = DateTimeOffset.MinValue;

    [JsonPropertyName("last_used_at")]
    public DateTimeOffset? LastUsedAt { get; init; }
        = null;
}

public record class CreateApiKeyResponseDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } 

    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("key")]
    public string Key { get; init; }

    [JsonPropertyName("prefix")]
    public string Prefix { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
        = DateTimeOffset.MinValue;
}

public record class CreateApiKeyRequestDto
{
    [Required]
    [JsonPropertyName("name")]
    public string Name { get; init; } 
}
