using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NetAI.RuntimeGateway.Services;

public sealed class SocketIoConnectionHandler
{
    private const string HttpClientName = nameof(SocketIoConnectionHandler);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SocketIoConnectionHandler> _logger;
    private readonly OpenHandsOptions _options;
    private readonly TimeSpan _socketConnectTimeout;

    public SocketIoConnectionHandler(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenHandsOptions> options,
        ILogger<SocketIoConnectionHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
        _socketConnectTimeout = CalculateConnectTimeout(_options);
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            await ProxyWebSocketAsync(context);
            return;
        }

        await ProxyHttpAsync(context);
    }

    private async Task ProxyHttpAsync(HttpContext context)
    {
        HttpRequestMessage upstreamRequest = await CreateUpstreamHttpRequestAsync(context);
        using HttpClient client = _httpClientFactory.CreateClient(HttpClientName);

        using HttpResponseMessage upstreamResponse = await client.SendAsync(
            upstreamRequest,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);

        context.Response.StatusCode = (int)upstreamResponse.StatusCode;
        CopyHeaders(upstreamResponse.Headers, context.Response.Headers);
        CopyHeaders(upstreamResponse.Content.Headers, context.Response.Headers);
        context.Response.Headers.Remove("transfer-encoding");

        await upstreamResponse.Content.CopyToAsync(context.Response.Body);
    }

    private async Task ProxyWebSocketAsync(HttpContext context)
    {
        using WebSocket clientSocket = await context.WebSockets.AcceptWebSocketAsync();
        using var upstreamSocket = new ClientWebSocket();

        ConfigureUpstreamHeaders(upstreamSocket, context.Request);

        Uri upstreamUri = BuildSocketUri(context.Request, useWebSocket: true);

        _logger.LogDebug(
            "Establishing upstream Socket.IO connection for request {TraceIdentifier} to {Uri}.",
            context.TraceIdentifier,
            upstreamUri);

        try
        {
            using var connectCts = new CancellationTokenSource(_socketConnectTimeout);
            await upstreamSocket.ConnectAsync(upstreamUri, connectCts.Token);
        }
        catch (OperationCanceledException ex)
        {
            bool requestCancelled = context.RequestAborted.IsCancellationRequested;
            string reason = requestCancelled
                ? "Request cancelled"
                : $"Upstream connection timed out after {_socketConnectTimeout.TotalSeconds:F0}s";
            _logger.LogError(
                ex,
                "Cancelled while connecting to upstream Socket.IO server at {Uri}. RequestAborted={IsRequestAborted}.",
                upstreamUri,
                requestCancelled);
            await clientSocket.CloseAsync(
                WebSocketCloseStatus.InternalServerError,
                reason,
                CancellationToken.None);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to upstream Socket.IO server at {Uri}", upstreamUri);
            await clientSocket.CloseAsync(
                WebSocketCloseStatus.InternalServerError,
                "Upstream connection failed",
                CancellationToken.None);
            return;
        }

        _logger.LogInformation(
            "Upstream Socket.IO connection established for request {TraceIdentifier} to {Uri}.",
            context.TraceIdentifier,
            upstreamUri);

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        CancellationToken cancellationToken = linkedCts.Token;

        Task clientToServer = PumpAsync(clientSocket, upstreamSocket, cancellationToken);
        Task serverToClient = PumpAsync(upstreamSocket, clientSocket, cancellationToken);

        Task completed = await Task.WhenAny(clientToServer, serverToClient);
        linkedCts.Cancel();

        if (completed == clientToServer && upstreamSocket.State == WebSocketState.Open)
        {
            _logger.LogDebug(
                "Client socket closed for request {TraceIdentifier}. Closing upstream socket with status {Status}.",
                context.TraceIdentifier,
                upstreamSocket.CloseStatus);
            await CloseSocketSafeAsync(
                upstreamSocket,
                WebSocketCloseStatus.NormalClosure,
                "Client closed",
                CancellationToken.None);
        }

        if (completed == serverToClient && clientSocket.State == WebSocketState.Open)
        {
            _logger.LogDebug(
                "Upstream socket closed for request {TraceIdentifier}. Closing client socket with status {Status}.",
                context.TraceIdentifier,
                clientSocket.CloseStatus);
            await CloseSocketSafeAsync(
                clientSocket,
                WebSocketCloseStatus.NormalClosure,
                "Server closed",
                CancellationToken.None);
        }

        await Task.WhenAll(SuppressExceptionsAsync(clientToServer), SuppressExceptionsAsync(serverToClient));
    }

    private async Task<HttpRequestMessage> CreateUpstreamHttpRequestAsync(HttpContext context)
    {
        var request = context.Request;
        Uri upstreamUri = BuildSocketUri(request, useWebSocket: false);

        var upstreamRequest = new HttpRequestMessage(new HttpMethod(request.Method), upstreamUri);

        if (request.ContentLength.HasValue && request.ContentLength.Value > 0)
        {
            var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, context.RequestAborted);
            buffer.Position = 0;
            upstreamRequest.Content = new StreamContent(buffer);
        }

        foreach (var header in request.Headers)
        {
            if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!upstreamRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                upstreamRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        return upstreamRequest;
    }

    private static void CopyHeaders(System.Net.Http.Headers.HttpHeaders source, IHeaderDictionary destination)
    {
        foreach (var header in source)
        {
            destination[header.Key] = header.Value.ToArray();
        }
    }

    private void ConfigureUpstreamHeaders(ClientWebSocket upstreamSocket, HttpRequest request)
    {
        foreach (var header in request.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Upgrade", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Sec-WebSocket-Version", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Sec-WebSocket-Extensions", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            upstreamSocket.Options.SetRequestHeader(header.Key, header.Value.ToString());
        }

        if (request.Headers.TryGetValue("Cookie", out var cookieValues))
        {
            var cookieContainer = new CookieContainer();
            cookieContainer.SetCookies(BuildSocketUri(request, useWebSocket: false), cookieValues.ToString());
            upstreamSocket.Options.Cookies = cookieContainer;
        }
    }

    private async Task PumpAsync(WebSocket source, WebSocket destination, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await source.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await CloseSocketSafeAsync(
                        destination,
                        result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                        result.CloseStatusDescription,
                        CancellationToken.None);
                    break;
                }

                await destination.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    cancellationToken);
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "Socket proxy pump error");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task CloseSocketSafeAsync(
        WebSocket socket,
        WebSocketCloseStatus closeStatus,
        string description,
        CancellationToken cancellationToken)
    {
        try
        {
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseOutputAsync(closeStatus, description, cancellationToken);
            }
        }
        catch (WebSocketException)
        {
            // Ignore errors while closing sockets.
        }
    }

    private async Task SuppressExceptionsAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (WebSocketException)
        {
            // Ignore transient socket errors on shutdown.
        }
    }

    private Uri BuildSocketUri(HttpRequest request, bool useWebSocket)
    {
        if (string.IsNullOrWhiteSpace(_options.SocketUrl))
        {
            throw new InvalidOperationException("OpenHands SocketUrl configuration is required.");
        }

        var baseUri = new Uri(_options.SocketUrl, UriKind.Absolute);
        var builder = new UriBuilder(baseUri);

        string incomingPath = request.Path.HasValue ? request.Path.Value! : string.Empty;
        string basePath = baseUri.AbsolutePath;

        if (!string.IsNullOrEmpty(basePath) && basePath != "/")
        {
            string trimmedBase = basePath.TrimEnd('/');
            string relativePath = incomingPath.StartsWith(trimmedBase, StringComparison.OrdinalIgnoreCase)
                ? incomingPath[trimmedBase.Length..]
                : incomingPath;

            builder.Path = CombinePaths(trimmedBase, relativePath);
        }
        else
        {
            builder.Path = string.IsNullOrEmpty(incomingPath) ? "/" : incomingPath;
        }

        string combinedQuery = CombineQueries(baseUri.Query, request.QueryString.HasValue ? request.QueryString.Value! : string.Empty);
        builder.Query = combinedQuery;

        if (useWebSocket)
        {
            builder.Scheme = baseUri.Scheme switch
            {
                "https" => "wss",
                "http" => "ws",
                "wss" => "wss",
                "ws" => "ws",
                _ => "ws"
            };
        }

        return builder.Uri;
    }

    private static TimeSpan CalculateConnectTimeout(OpenHandsOptions options)
    {
        const int DefaultSeconds = 30;
        const int MaxSeconds = 300;

        int configured = options.SocketConnectionTimeoutSeconds;
        if (configured <= 0)
        {
            configured = DefaultSeconds;
        }

        if (configured > MaxSeconds)
        {
            configured = MaxSeconds;
        }

        return TimeSpan.FromSeconds(configured);
    }

    private static string CombinePaths(string basePath, string additionalPath)
    {
        string normalizedBase = string.IsNullOrEmpty(basePath) ? string.Empty : basePath.TrimEnd('/');
        string normalizedAdditional = string.IsNullOrEmpty(additionalPath) ? string.Empty : additionalPath.TrimStart('/');

        if (string.IsNullOrEmpty(normalizedAdditional))
        {
            return string.IsNullOrEmpty(normalizedBase) ? "/" : normalizedBase + "/";
        }

        return string.IsNullOrEmpty(normalizedBase)
            ? "/" + normalizedAdditional
            : normalizedBase + "/" + normalizedAdditional;
    }

    private static string CombineQueries(string baseQuery, string incomingQuery)
    {
        string trimmedBase = baseQuery.TrimStart('?');
        string trimmedIncoming = incomingQuery.TrimStart('?');

        if (string.IsNullOrEmpty(trimmedBase))
        {
            return trimmedIncoming;
        }

        if (string.IsNullOrEmpty(trimmedIncoming))
        {
            return trimmedBase;
        }

        return $"{trimmedBase}&{trimmedIncoming}";
    }
}
