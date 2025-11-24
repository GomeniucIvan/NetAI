using System;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetAI.Api.Models.Mcp;
using NetAI.Api.Services.Git;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("mcp")]
public class McpController : ControllerBase
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMcpGitService _gitService;
    private readonly ILogger<McpController> _logger;

    public McpController(IMcpGitService gitService, ILogger<McpController> logger)
    {
        _gitService = gitService;
        _logger = logger;
    }

    [HttpPost("mcp")]
    public async Task<ActionResult<McpJsonRpcResponse>> CallTool(
        [FromBody] McpJsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        string conversationId = Request.Headers["X-OpenHands-ServerConversation-ID"].FirstOrDefault();
        McpJsonRpcResponse response;

        try
        {
            if (!string.Equals(request.Method, "tools/call", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"Unsupported MCP method '{request.Method}'.");
            }

            string toolName = request.Params?.Name ?? string.Empty;
            string result = toolName switch
            {
                "create_pr" => await _gitService.CreatePullRequestAsync(
                    Deserialize<CreatePullRequestRequest>(request.Params.Arguments),
                    conversationId,
                    cancellationToken).ConfigureAwait(false),
                "create_mr" => await _gitService.CreateMergeRequestAsync(
                    Deserialize<CreateMergeRequestRequest>(request.Params.Arguments),
                    conversationId,
                    cancellationToken).ConfigureAwait(false),
                "create_bitbucket_pr" => await _gitService.CreateBitbucketPullRequestAsync(
                    Deserialize<CreateBitbucketPullRequestRequest>(request.Params.Arguments),
                    conversationId,
                    cancellationToken).ConfigureAwait(false),
                _ => throw new NotSupportedException($"Unsupported MCP tool '{toolName}'.")
            };

            response = CreateSuccessResponse(request.Id, result);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse MCP tool arguments for request id {RequestId}", request.Id);
            response = CreateErrorResponse(request.Id, -32602, "Invalid tool arguments.");
        }
        catch (GitAuthorizationException ex)
        {
            _logger.LogWarning(ex, "Git authorization failure for MCP request id {RequestId}", request.Id);
            response = CreateErrorResponse(request.Id, -32001, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Unsupported MCP request {Message}", ex.Message);
            response = CreateErrorResponse(request.Id, -32601, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing MCP tool {Tool}", request.Params?.Name);
            response = CreateErrorResponse(request.Id, -32000, $"Error executing MCP tool: {ex.Message}");
        }

        return Ok(response);
    }


    //TODO extensions
    private static T Deserialize<T>(JsonElement element)
    {
        T value = element.Deserialize<T>(SerializerOptions);
        if (value is null)
        {
            throw new JsonException($"Failed to deserialize MCP arguments to {typeof(T).Name}.");
        }

        return value;
    }

    //TODO esp
    private static McpJsonRpcResponse CreateSuccessResponse(string id, string result)
    {
        return new McpJsonRpcResponse
        {
            Id = id,
            Result = new McpToolResultDto
            {
                Content = new[]
                {
                    new McpToolContentDto
                    {
                        Type = "text",
                        Text = result
                    }
                }
            }
        };
    }
    //TODO esp
    private static McpJsonRpcResponse CreateErrorResponse(string id, int code, string message)
    {
        return new McpJsonRpcResponse
        {
            Id = id,
            Error = new McpJsonRpcErrorDto
            {
                Code = code,
                Message = message
            }
        };
    }
}
