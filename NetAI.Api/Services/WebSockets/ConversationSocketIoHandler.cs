using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Services.Conversations;

namespace NetAI.Api.Services.WebSockets;

public sealed class ConversationSocketIoHandler
{
    private const int DefaultEventFetchLimit = 100;

    private readonly IConversationSessionService _conversationService;
    private readonly IConversationEventNotifier _eventNotifier;
    private readonly ILogger<ConversationSocketIoHandler> _logger;

    public ConversationSocketIoHandler(
        IConversationSessionService conversationService,
        IConversationEventNotifier eventNotifier,
        ILogger<ConversationSocketIoHandler> logger)
    {
        _conversationService = conversationService;
        _eventNotifier = eventNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
    {
        HttpRequest request = context.Request;
        string conversationId = request.Query["conversation_id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        string sessionApiKey = request.Query["session_api_key"].FirstOrDefault();
        string latestEventIdRaw = request.Query["latest_event_id"].FirstOrDefault();
        int latestEventId = TryParseEventId(latestEventIdRaw);

        IReadOnlyList<string> backlog = Array.Empty<string>();

        try
        {
            backlog = await FetchBacklogAsync(conversationId, sessionApiKey, latestEventId, cancellationToken)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare WebSocket backlog for conversation {ConversationId}", conversationId);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        WebSocket socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        var connection = new ConversationSocketIoConnection(
            socket,
            conversationId,
            sessionApiKey,
            backlog,
            _conversationService,
            _eventNotifier,
            _logger);

        await connection.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    private static int TryParseEventId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return -1;
        }

        return int.TryParse(value, out int parsed) ? parsed : -1;
    }

    private async Task<IReadOnlyList<string>> FetchBacklogAsync(
        string conversationId,
        string sessionApiKey,
        int latestEventId,
        CancellationToken cancellationToken)
    {
        int startId = latestEventId >= 0 ? latestEventId + 1 : 0;
        ConversationEventsPageDto page = await _conversationService
            .GetEventsAsync(
                conversationId,
                sessionApiKey,
                startId,
                endId: null,
                reverse: false,
                limit: DefaultEventFetchLimit,
                excludeHidden: true,
                cancellationToken)
            .ConfigureAwait(false);

        if (page.Events.Count == 0)
        {
            return Array.Empty<string>();
        }

        var payloads = new List<string>(page.Events.Count);
        foreach (ConversationEventDto evt in page.Events.OrderBy(e => e.Id))
        {
            try
            {
                payloads.Add(evt.Event.GetRawText());
            }
            catch (InvalidOperationException)
            {
                // Ignore malformed payloads
            }
        }

        return payloads;
    }
}

internal sealed class ConversationSocketIoConnection
{
    private static readonly TimeSpan PongTimeout = TimeSpan.FromSeconds(20);

    private readonly WebSocket _socket;
    private readonly string _conversationId;
    private readonly string _sessionApiKey;
    private readonly IReadOnlyList<string> _backlog;
    private readonly IConversationSessionService _conversationService;
    private readonly IConversationEventNotifier _eventNotifier;
    private readonly ILogger _logger;

    public ConversationSocketIoConnection(
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
            await SendHandshakeAsync(linkedToken).ConfigureAwait(false);
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
            await CloseSocketAsync("closing", CancellationToken.None).ConfigureAwait(false);
        }
    }


    private Task SendHandshakeAsync(CancellationToken cancellationToken)
    {
        string sid = Guid.NewGuid().ToString("N");
        var handshakePayload = new
        {
            sid,
            upgrades = Array.Empty<string>(),
            pingInterval = 25000,
            pingTimeout = (int)PongTimeout.TotalMilliseconds
        };

        string handshakeMessage = $"0{JsonSerializer.Serialize(handshakePayload)}";
        _logger.LogInformation(">>> Sending handshake message: {Handshake}", handshakeMessage);

        return SendTextAsync(new[]
        {
            handshakeMessage,
            "40"
        }, cancellationToken);
    }

    private async Task SendBacklogAsync(CancellationToken cancellationToken)
    {
        foreach (string payload in _backlog)
        {
            await SendEventAsync("oh_event", payload, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ForwardEventsAsync(ChannelReader<string> reader, CancellationToken cancellationToken)
    {
        await foreach (string payload in reader.ReadAllAsync(cancellationToken))
        {
            await SendEventAsync("oh_event", payload, cancellationToken).ConfigureAwait(false);
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
                var result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage)
                {
                    continue;
                }

                string message = builder.ToString();
                builder.Clear();

                if (string.IsNullOrEmpty(message))
                {
                    continue;
                }

                await HandleMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket receive loop ended for conversation {ConversationId}", _conversationId);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            linkedSource.Cancel();
        }
    }

    private async Task HandleMessageAsync(string message, CancellationToken cancellationToken)
    {
        switch (message[0])
        {
            case '2':
                await SendTextAsync("3", cancellationToken).ConfigureAwait(false);
                break;
            case '4':
                await HandleSocketIoPayloadAsync(message, cancellationToken).ConfigureAwait(false);
                break;
            default:
                break;
        }
    }

    private async Task HandleSocketIoPayloadAsync(string message, CancellationToken cancellationToken)
    {
        if (message.Length < 2)
        {
            return;
        }

        char code = message[1];
        if (code == '1')
        {
            // Client initiated namespace close
            await CloseSocketAsync("client closed", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (code != '2')
        {
            return;
        }

        string payload = message[2..];
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            return;
        }

        string eventName = document.RootElement[0].GetString();
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        if (document.RootElement.GetArrayLength() < 2)
        {
            return;
        }

        JsonElement dataElement = document.RootElement[1];
        switch (eventName)
        {
            case "oh_user_action":
            case "oh_action":
                await ForwardUserEventAsync(dataElement.Clone(), cancellationToken).ConfigureAwait(false);
                break;
            default:
                break;
        }
    }

    private async Task CloseSocketAsync(string description, CancellationToken cancellationToken)
    {
        try
        {
            switch (_socket.State)
            {
                case WebSocketState.Open:
                    await _socket
                        .CloseAsync(WebSocketCloseStatus.NormalClosure, description, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case WebSocketState.CloseReceived:
                    await _socket
                        .CloseOutputAsync(WebSocketCloseStatus.NormalClosure, description, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                default:
                    return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "WebSocket close handshake failed for conversation {ConversationId}", _conversationId);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "WebSocket close skipped because the connection is already closed for conversation {ConversationId}", _conversationId);
        }
    }

    private async Task ForwardUserEventAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        try
        {
            await _conversationService
                .AddEventAsync(_conversationId, _sessionApiKey, payload, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to forward user event to conversation {ConversationId}",
                _conversationId);
        }
    }

    private Task SendEventAsync(string eventName, string payloadJson, CancellationToken cancellationToken)
    {
        string encodedEventName = JsonSerializer.Serialize(eventName);
        string message = $"42[{encodedEventName},{payloadJson}]";
        return SendTextAsync(message, cancellationToken);
    }

    private async Task SendTextAsync(string message, CancellationToken cancellationToken)
    {
        if (_socket.State != WebSocketState.Open)
        {
            return;
        }

        try
        {
            await _socket
                .SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "WebSocket send failed for conversation {ConversationId}", _conversationId);
        }
    }

    private async Task SendTextAsync(IEnumerable<string> messages, CancellationToken cancellationToken)
    {
        foreach (string message in messages)
        {
            await SendTextAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }
}
