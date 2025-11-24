using System.Text.Json.Serialization;

namespace NetAI.Api.Models.User;

public record class GitUserDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("login")]
    public string Login { get; init; } 

    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; init; }

    [JsonPropertyName("company")]
    public string Company { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("email")]
    public string Email { get; init; }
}
