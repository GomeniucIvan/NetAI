using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Mcp;

public record class McpJsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("method")]
    public string Method { get; init; }

    [JsonPropertyName("params")]
    public McpToolCallParameters Params { get; init; } = new();
}

public record class McpToolCallParameters
{
    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; init; }
        = default;
}

public record class McpToolContentDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; init; }
}

public record class McpToolResultDto
{
    [JsonPropertyName("content")]
    public IReadOnlyList<McpToolContentDto> Content { get; init; }
        = Array.Empty<McpToolContentDto>();
}

public record class McpJsonRpcErrorDto
{
    [JsonPropertyName("code")]
    public int Code { get; init; }
        = -32000;

    [JsonPropertyName("message")]
    public string Message { get; init; }
}

public record class McpJsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("result")]
    public McpToolResultDto Result { get; init; }

    [JsonPropertyName("error")]
    public McpJsonRpcErrorDto Error { get; init; }
}

public record class CreatePullRequestRequest
{
    [JsonPropertyName("repo_name")]
    public string Repository { get; init; }

    [JsonPropertyName("source_branch")]
    public string SourceBranch { get; init; }

    [JsonPropertyName("target_branch")]
    public string TargetBranch { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; }

    [JsonPropertyName("body")]
    public string Body { get; init; }

    [JsonPropertyName("draft")]
    public bool Draft { get; init; } = true;

    [JsonPropertyName("labels")]
    public IReadOnlyList<string> Labels { get; init; }
}

public record class CreateMergeRequestRequest
{
    [JsonPropertyName("id")]
    public JsonElement ProjectId { get; init; }
        = default;

    [JsonPropertyName("source_branch")]
    public string SourceBranch { get; init; }

    [JsonPropertyName("target_branch")]
    public string TargetBranch { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; }

    [JsonPropertyName("labels")]
    public IReadOnlyList<string> Labels { get; init; }

    public string ResolveProjectId()
    {
        if (ProjectId.ValueKind == JsonValueKind.Number)
        {
            return ProjectId.GetRawText();
        }

        if (ProjectId.ValueKind == JsonValueKind.String)
        {
            string value = ProjectId.GetString();
            return value is null
                ? string.Empty
                : Uri.EscapeDataString(value);
        }

        throw new JsonException("GitLab project id must be a string or number.");
    }
}

public record class CreateBitbucketPullRequestRequest
{
    [JsonPropertyName("repo_name")]
    public string Repository { get; init; } 

    [JsonPropertyName("source_branch")]
    public string SourceBranch { get; init; } 

    [JsonPropertyName("target_branch")]
    public string TargetBranch { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; }

    [JsonPropertyName("draft")]
    public bool Draft { get; init; }
}
