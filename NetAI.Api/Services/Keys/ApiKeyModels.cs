namespace NetAI.Api.Services.Keys;

public record class ApiKeyRecord
{
    public Guid Id { get; init; }
        = Guid.Empty;

    public string Name { get; init; }

    public string Prefix { get; init; }

    public string HashedKey { get; init; } 

    public DateTimeOffset CreatedAt { get; init; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastUsedAt { get; init; }

    public ApiKeyRecord Copy()
        => this with { };
}
