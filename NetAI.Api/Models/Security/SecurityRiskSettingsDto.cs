using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Security;

public class SecurityRiskSettingsDto
{
    [JsonPropertyName("RISK_SEVERITY")]
    public int RiskSeverity { get; set; }
}
