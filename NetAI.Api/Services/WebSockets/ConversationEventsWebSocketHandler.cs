using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Services.Conversations;

namespace NetAI.Api.Services.WebSockets;

public sealed class ConversationEventsWebSocketHandler
{
    private const int BacklogPageSize = 100;

    private readonly IConversationSessionService _conversationService;
    private readonly IConversationEventNotifier _eventNotifier;
    private readonly ILogger<ConversationEventsWebSocketHandler> _logger;

    public ConversationEventsWebSocketHandler(
        IConversationSessionService conversationService,
        IConversationEventNotifier eventNotifier,
        ILogger<ConversationEventsWebSocketHandler> logger)
    {
        _conversationService = conversationService;
        _eventNotifier = eventNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context, string conversationId, CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        HttpRequest request = context.Request;
        string sessionApiKey = request.Query["session_api_key"].FirstOrDefault();
        bool resendAll = TryParseBoolean(request.Query["resend_all"].FirstOrDefault());
        int latestEventId = TryParseEventId(request.Query["latest_event_id"].FirstOrDefault());

        IReadOnlyList<string> backlog;

        try
        {
            backlog = await FetchBacklogAsync(
                    conversationId,
                    sessionApiKey,
                    resendAll,
                    latestEventId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ConversationUnauthorizedException)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        catch (ConversationNotFoundException)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        catch (ConversationRuntimeUnavailableException ex)
        {
            _logger.LogWarning(
                ex,
                "Runtime unavailable when preparing backlog for conversation {ConversationId}",
                conversationId);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to prepare WebSocket backlog for conversation {ConversationId}",
                conversationId);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        WebSocket socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);

        var connection = new ConversationEventsWebSocketConnection(
            socket,
            conversationId,
            sessionApiKey,
            backlog,
            _conversationService,
            _eventNotifier,
            _logger);

        await connection.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<string>> FetchBacklogAsync(
        string conversationId,
        string sessionApiKey,
        bool resendAll,
        int latestEventId,
        CancellationToken cancellationToken)
    {
        var payloads = new List<string>();

        int nextStartId = resendAll ? 0 : latestEventId >= 0 ? latestEventId + 1 : 0;
        const int MaxFetchIterations = 100;
        int iteration = 0;

        while (true)
        {
            ConversationEventsPageDto page = await _conversationService
                .GetEventsAsync(
                    conversationId,
                    sessionApiKey,
                    nextStartId,
                    endId: null,
                    reverse: false,
                    limit: BacklogPageSize,
                    excludeHidden: true,
                    cancellationToken)
                .ConfigureAwait(false);

            if (page.Events.Count == 0)
            {
                break;
            }

            foreach (ConversationEventDto evt in page.Events.OrderBy(e => e.Id))
            {
                try
                {
                    payloads.Add(evt.Event.GetRawText());
                }
                catch (InvalidOperationException)
                {
                    // Ignore 
                }
            }

            if (!resendAll)
            {
                break;
            }

            if (!page.HasMore)
            {
                break;
            }

            int maxId = page.Events.Max(e => e.Id);
            if (maxId < nextStartId)
            {
                // Prevent potential infinite loops if IDs are not increasing
                break;
            }

            nextStartId = maxId + 1;
            iteration++;
            if (iteration >= MaxFetchIterations)
            {
                _logger.LogWarning(
                    "Reached backlog fetch iteration limit for conversation {ConversationId}. Some events may be omitted.",
                    conversationId);
                break;
            }
        }

        return payloads;
    }

    //todo est
    private static bool TryParseBoolean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    //todo est
    private static int TryParseEventId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return -1;
        }

        return int.TryParse(value, out int parsed) ? parsed : -1;
    }
}

//todo re
internal sealed class ConversationEventsWebSocketConnection
{
    private readonly WebSocket _socket;
    private readonly string _conversationId;
    private readonly string _sessionApiKey;
    private readonly IReadOnlyList<string> _backlog;
    private readonly IConversationSessionService _conversationService;
    private readonly IConversationEventNotifier _eventNotifier;
    private readonly ILogger _logger;

    public ConversationEventsWebSocketConnection(
        WebSocket socket,
        string conversationId,
        string sessionApiKey,
        IReadOnlyList<string> backlog,
        IConversationSessionService conversationService,
        IConversationEventNotifier eventNotifier,
        ILogger logger)
    {
        _socket = socket;
        _conversationId = conversationId;
        _sessionApiKey = sessionApiKey;
        _backlog = backlog;
        _conversationService = conversationService;
        _eventNotifier = eventNotifier;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken linkedToken = linkedSource.Token;

        try
        {
            await SendBacklogAsync(linkedToken).ConfigureAwait(false);

            await using ConversationEventSubscription subscription = _eventNotifier.Subscribe(_conversationId, linkedToken);

            Task receiveLoop = ReceiveLoopAsync(linkedSource);
            Task sendLoop = ForwardEventsAsync(subscription.Reader, linkedToken);

            await Task.WhenAny(receiveLoop, sendLoop).ConfigureAwait(false);
            linkedSource.Cancel();

            try
            {
                await Task.WhenAll(receiveLoop, sendLoop).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
            {
            }
        }
        finally
        {
            linkedSource.Cancel();
            linkedSource.Dispose();
            if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
            {
                await _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "closing",
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task SendBacklogAsync(CancellationToken cancellationToken)
    {
        foreach (string payload in _backlog)
        {
            await SendTextAsync(payload, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ForwardEventsAsync(ChannelReader<string> reader, CancellationToken cancellationToken)
    {
        await foreach (string payload in reader.ReadAllAsync(cancellationToken))
        {
            await SendTextAsync(payload, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(CancellationTokenSource linkedSource)
    {
        CancellationToken cancellationToken = linkedSource.Token;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);

        try
        {
            var builder = new StringBuilder();
            while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await _socket
                    .ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                    .ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage)
                {
                    continue;
                }

                string message = builder.ToString();
                builder.Clear();

                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                await HandleIncomingMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(
                ex,
                "WebSocket receive loop ended for conversation {ConversationId}",
                _conversationId);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            linkedSource.Cancel();
        }
    }

    private async Task HandleIncomingMessageAsync(string message, CancellationToken cancellationToken)
    {
        string text = ExtractUserMessage(message);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            var request = new ConversationMessageRequestDto { Message = text };
            await _conversationService
                .AddMessageAsync(_conversationId, _sessionApiKey, request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ConversationSessionException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch message for conversation {ConversationId}",
                _conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while processing message for conversation {ConversationId}",
                _conversationId);
        }
    }

    private Task SendTextAsync(string payload, CancellationToken cancellationToken)
    {
        if (_socket.State != WebSocketState.Open)
        {
            return Task.CompletedTask;
        }

        byte[] data = Encoding.UTF8.GetBytes(payload);
        return _socket.SendAsync(data, WebSocketMessageType.Text, true, cancellationToken);
    }

    private static string ExtractUserMessage(string message)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(message);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!root.TryGetProperty("role", out JsonElement roleElement))
            {
                return null;
            }

            string role = roleElement.GetString();
            if (!string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!root.TryGetProperty("content", out JsonElement contentElement)
                || contentElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var builder = new StringBuilder();
            foreach (JsonElement entry in contentElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!entry.TryGetProperty("type", out JsonElement typeElement))
                {
                    continue;
                }

                string type = typeElement.GetString();
                if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
                {
                    if (entry.TryGetProperty("text", out JsonElement textElement))
                    {
                        string text = textElement.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            if (builder.Length > 0)
                            {
                                builder.AppendLine();
                            }

                            builder.Append(text);
                        }
                    }
                }
                else if (string.Equals(type, "image_url", StringComparison.OrdinalIgnoreCase))
                {
                    if (entry.TryGetProperty("image_url", out JsonElement imageElement)
                        && imageElement.ValueKind == JsonValueKind.Object
                        && imageElement.TryGetProperty("url", out JsonElement urlElement))
                    {
                        string url = urlElement.GetString();
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            if (builder.Length > 0)
                            {
                                builder.AppendLine();
                            }

                            builder.Append($"Image: {url}");
                        }
                    }
                }
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
