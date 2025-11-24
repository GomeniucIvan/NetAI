using System.Text.Json.Serialization;

namespace NetAI.SandboxOrchestration.Models;

public class RuntimeOperationResponse
{
    [JsonPropertyName("runtime_id")]
    public string RuntimeId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Code { get; set; }

    [JsonIgnore]
    public bool Succeeded { get; set; }

    public static RuntimeOperationResponse Success(string runtimeId, string status, string message = null)
        => new()
        {
            RuntimeId = runtimeId,
            Status = status,
            Message = message ?? string.Empty,
            Succeeded = true
        };

    public static RuntimeOperationResponse Failure(string runtimeId, string message, string status = "error")
        => new()
        {
            RuntimeId = runtimeId,
            Status = status,
            Message = message,
            Succeeded = false
        };
}
