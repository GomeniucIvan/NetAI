using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Conversations;

public class FeedbackDto
{
    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; }

    [JsonPropertyName("polarity")]
    public string Polarity { get; set; }

    [JsonPropertyName("permissions")]
    public string Permissions { get; set; } = "private";

    [JsonPropertyName("trajectory")]
    public IList<object> Trajectory { get; set; } = new List<object>();
}

public class FeedbackBodyResponseDto
{
    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("feedback_id")]
    public string FeedbackId { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; }
}

public class FeedbackResponseDto
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("body")]
    public FeedbackBodyResponseDto Body { get; set; } = new FeedbackBodyResponseDto();
}
