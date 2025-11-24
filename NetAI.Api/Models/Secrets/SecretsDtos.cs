using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Secrets;

public record class CustomSecretDto
{
    [Required]
    [JsonPropertyName("name")]
    public string Name { get; init; }

    [Required]
    [JsonPropertyName("value")]
    public string Value { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; }
}

public record class CustomSecretWithoutValueDto
{
    [Required]
    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; }
}

public record class GetSecretsResponseDto
{
    [JsonPropertyName("custom_secrets")]
    public IReadOnlyList<CustomSecretWithoutValueDto> CustomSecrets { get; init; }
        = Array.Empty<CustomSecretWithoutValueDto>();
}

public record class ProviderTokenDto
{
    [JsonPropertyName("token")]
    public string Token { get; init; }

    [JsonPropertyName("host")]
    public string Host { get; init; }
}

public record class ProviderTokensRequestDto
{
    [JsonPropertyName("provider_tokens")]
    public IDictionary<string, ProviderTokenDto> ProviderTokens { get; init; }
        = new Dictionary<string, ProviderTokenDto>(StringComparer.OrdinalIgnoreCase);
}
