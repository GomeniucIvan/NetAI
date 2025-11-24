using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Installation;

public class InstallRequestDto
{
    [JsonPropertyName("connectionString")]
    public string ConnectionString { get; init; }

    [JsonPropertyName("host")]
    public string Host { get; init; }

    [JsonPropertyName("port")]
    public int? Port { get; init; }
        = 5432;

    [JsonPropertyName("database")]
    public string Database { get; init; }

    [JsonPropertyName("username")]
    public string Username { get; init; }

    [JsonPropertyName("password")]
    public string Password { get; init; }

    [JsonPropertyName("dataSource")]
    public string DataSource { get; init; }
}
