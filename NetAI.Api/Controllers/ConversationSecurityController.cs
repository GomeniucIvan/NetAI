using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetAI.Api.Models;
using NetAI.Api.Services.Conversations;

namespace NetAI.Api.Controllers;

[ApiController]
[Route("api/conversations/{conversationId}/security")]
public class ConversationSecurityController : ControllerBase
{
    private static readonly HashSet<string> MethodsWithBody = new(
        new[] { HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete },
        StringComparer.OrdinalIgnoreCase);

    private readonly IConversationSessionService _conversationService;
    private readonly IHttpClientFactory _httpClientFactory;

    public ConversationSecurityController(
        IConversationSessionService conversationService,
        IHttpClientFactory httpClientFactory)
    {
        _conversationService = conversationService;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    [HttpGet("{*path}")]
    public Task<IActionResult> ProxyGet(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        [FromRoute(Name = "path")] string path,
        CancellationToken cancellationToken = default)
    {
        return ProxyAsync(HttpMethod.Get, conversationId, sessionApiKey, path, cancellationToken);
    }

    [HttpPost]
    [HttpPost("{*path}")]
    public Task<IActionResult> ProxyPost(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        [FromRoute(Name = "path")] string path,
        CancellationToken cancellationToken = default)
    {
        return ProxyAsync(HttpMethod.Post, conversationId, sessionApiKey, path, cancellationToken);
    }

    [HttpPut]
    [HttpPut("{*path}")]
    public Task<IActionResult> ProxyPut(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        [FromRoute(Name = "path")] string path,
        CancellationToken cancellationToken = default)
    {
        return ProxyAsync(HttpMethod.Put, conversationId, sessionApiKey, path, cancellationToken);
    }

    [HttpDelete]
    [HttpDelete("{*path}")]
    public Task<IActionResult> ProxyDelete(
        string conversationId,
        [FromHeader(Name = "X-Session-API-Key")] string sessionApiKey,
        [FromRoute(Name = "path")] string path,
        CancellationToken cancellationToken = default)
    {
        return ProxyAsync(HttpMethod.Delete, conversationId, sessionApiKey, path, cancellationToken);
    }

    private async Task<IActionResult> ProxyAsync(
        HttpMethod method,
        string conversationId,
        string sessionApiKey,
        string path,
        CancellationToken cancellationToken)
    {
        string analyzerBaseUrl;
        try
        {
            analyzerBaseUrl = await _conversationService
                .GetSecurityAnalyzerUrlAsync(conversationId, sessionApiKey, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ConversationUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ConversationNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationResourceNotFoundException)
        {
            return NotFound();
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ErrorResponseDto { Error = ex.Reason });
        }
        catch (ConversationRuntimeActionException ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ErrorResponseDto { Error = ex.Reason });
        }

        Uri targetUri;
        try
        {
            targetUri = BuildTargetUri(analyzerBaseUrl, path, Request.QueryString.Value);
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ErrorResponseDto { Error = $"Invalid security analyzer endpoint: {ex.Message}" });
        }

        using HttpRequestMessage proxyRequest = new(method, targetUri);

        if (MethodsWithBody.Contains(Request.Method))
        {
            proxyRequest.Content = new StreamContent(Request.Body);
        }

        foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in Request.Headers)
        {
            if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase)
                || string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] values = header.Value.ToArray();

            if (!proxyRequest.Headers.TryAddWithoutValidation(header.Key, values))
            {
                proxyRequest.Content?.Headers.TryAddWithoutValidation(header.Key, values);
            }
        }

        HttpClient client = _httpClientFactory.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;

        HttpResponseMessage proxiedResponse;
        try
        {
            proxiedResponse = await client
                .SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new ErrorResponseDto { Error = ex.Message });
        }

        using (proxiedResponse)
        {
            Response.StatusCode = (int)proxiedResponse.StatusCode;

            Response.Headers.Remove("transfer-encoding");
            Response.Headers.Remove("Transfer-Encoding");
            Response.Headers.Remove("content-length");
            Response.Headers.Remove("Content-Length");

            foreach (KeyValuePair<string, IEnumerable<string>> header in proxiedResponse.Headers)
            {
                WriteResponseHeader(header);
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in proxiedResponse.Content.Headers)
            {
                WriteResponseHeader(header);
            }

            await proxiedResponse.Content.CopyToAsync(Response.Body, cancellationToken).ConfigureAwait(false);
        }

        return new EmptyResult();
    }

    private static Uri BuildTargetUri(string baseUrl, string path, string queryString)
    {
        var builder = new UriBuilder(baseUrl);
        string basePath = builder.Path ?? string.Empty;
        string trimmedBasePath = string.IsNullOrEmpty(basePath)
            ? string.Empty
            : basePath.TrimEnd('/');
        string relativePath = string.IsNullOrWhiteSpace(path) ? string.Empty : path.TrimStart('/');

        if (string.IsNullOrEmpty(trimmedBasePath))
        {
            builder.Path = string.IsNullOrEmpty(relativePath) ? "/" : $"/{relativePath}";
        }
        else if (string.IsNullOrEmpty(relativePath))
        {
            builder.Path = trimmedBasePath;
        }
        else
        {
            builder.Path = $"{trimmedBasePath}/{relativePath}";
        }

        if (!string.IsNullOrEmpty(queryString))
        {
            builder.Query = queryString.TrimStart('?');
        }

        return builder.Uri;
    }

    private void WriteResponseHeader(KeyValuePair<string, IEnumerable<string>> header)
    {
        if (string.Equals(header.Key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string value in header.Value)
            {
                Response.Headers.Append(header.Key, value);
            }

            return;
        }

        Response.Headers[header.Key] = header.Value.ToArray();
    }
}
