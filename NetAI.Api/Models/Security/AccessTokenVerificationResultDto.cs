using System.Text.Json.Serialization;
using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Models.Security;

public class AccessTokenVerificationResultDto
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; }

    [JsonPropertyName("provider_type")]
    public ProviderType ProviderType { get; set; }
}
