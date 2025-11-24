using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Security;

public class SecurityPolicyResponseDto
{
    [JsonPropertyName("policy")]
    public string Policy { get; set; }
}

public class UpdateSecurityPolicyRequestDto
{
    [JsonPropertyName("policy")]
    public string Policy { get; set; }
}
