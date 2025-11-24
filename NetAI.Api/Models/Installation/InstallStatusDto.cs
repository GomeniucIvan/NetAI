using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Installation;

public class InstallStatusDto
{
    private InstallStatusDto()
    {
    }

    [JsonPropertyName("isConfigured")]
    public bool IsConfigured { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; }

    public static InstallStatusDto Configured()
        => new()
        {
            IsConfigured = true,
            Message = null
        };

    public static InstallStatusDto NotConfigured(string message)
        => new()
        {
            IsConfigured = false,
            Message = message
        };
}
