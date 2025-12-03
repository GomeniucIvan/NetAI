using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.WebSockets;
using NetAI.RuntimeGateway.Services;

var builder = WebApplication.CreateBuilder(args);

// Load centralized ports configuration from solution root
var solutionRoot = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "..")
);

var portsConfigPath = Path.Combine(solutionRoot, "backend-ports.json");
if (File.Exists(portsConfigPath))
{
    builder.Configuration.AddJsonFile(portsConfigPath, optional: true, reloadOnChange: true);
}

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// HTTPS endpoint - Read from centralized config with fallback to appsettings
string host = builder.Configuration.GetValue<string>("BackendPorts:NetAI.RuntimeGateway:Host") 
           ?? builder.Configuration.GetValue<string>("Host") 
           ?? "127.0.0.1";
int port = builder.Configuration.GetValue<int?>("BackendPorts:NetAI.RuntimeGateway:Port") 
        ?? builder.Configuration.GetValue("Port", 7250);
bool useHttps = builder.Configuration.GetValue<bool?>("BackendPorts:NetAI.RuntimeGateway:UseHttps") 
             ?? builder.Configuration.GetValue("UseHttps", false);

builder.WebHost.ConfigureKestrel(options =>
{
    if (!IPAddress.TryParse(host, out IPAddress address))
    {
        address = IPAddress.Any;
    }

    if (useHttps)
    {
        options.Listen(address, port, listenOptions => listenOptions.UseHttps());
        Console.WriteLine($"RuntimeGateway listening on https://{host}:{port}");
    }
    else
    {
        options.Listen(address, port);
        Console.WriteLine($"RuntimeGateway listening on http://{host}:{port}");
    }
});

// Allow frontend (port 3001) to connect via Socket.IO
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
    new[] { "http://localhost:3000", "http://localhost:3001" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<OpenHandsOptions>(builder.Configuration.GetSection("OpenHands"));
builder.Services.PostConfigure<OpenHandsOptions>(options =>
{
    string configuredBase = builder.Configuration.GetValue<string>("ServiceUrls:NetAI.RuntimeServer");
    if (string.Equals(options.Provider, "netai", StringComparison.OrdinalIgnoreCase))
    {
        string runtimeHost = builder.Configuration.GetValue<string>("BackendPorts:NetAI.Server:Host") ?? "127.0.0.1";
        int runtimePort = builder.Configuration.GetValue<int?>("BackendPorts:NetAI.Server:Port") ?? 7252;
        bool runtimeHttps = builder.Configuration.GetValue<bool?>("BackendPorts:NetAI.Server:UseHttps") ?? false;
        configuredBase = $"{(runtimeHttps ? "https" : "http")}://{runtimeHost}:{runtimePort}";
        if (string.IsNullOrWhiteSpace(options.ApiPrefix))
        {
            options.ApiPrefix = "/api/runtime";
        }
    }
    else
    {
        if (string.IsNullOrWhiteSpace(configuredBase))
        {
            string runtimeHost = builder.Configuration.GetValue<string>("BackendPorts:NetAI.RuntimeServer:Host") ?? "127.0.0.1";
            int runtimePort = builder.Configuration.GetValue<int?>("BackendPorts:NetAI.RuntimeServer:Port") ?? 3000;
            bool runtimeHttps = builder.Configuration.GetValue<bool?>("BackendPorts:NetAI.RuntimeServer:UseHttps") ?? false;
            configuredBase = $"{(runtimeHttps ? "https" : "http")}://{runtimeHost}:{runtimePort}";
        }

        if (string.IsNullOrWhiteSpace(options.ApiPrefix))
        {
            options.ApiPrefix = "/api";
        }
    }

    if (string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        options.BaseUrl = configuredBase!;
    }

    Console.WriteLine($"[RuntimeGateway] OpenHands BaseUrl resolved to {options.BaseUrl} with ApiPrefix={options.ApiPrefix}");
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<IRuntimeConversationStore, RuntimeConversationStore>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<OpenHandsOptions>>().Value;
    client.BaseAddress = BuildApiBaseAddress(options);

    Console.WriteLine($"[RuntimeGateway] IRuntimeConversationStore client BaseAddress = {client.BaseAddress}");
});

builder.Services.AddHttpClient(nameof(SocketIoConnectionHandler));
builder.Services.AddSingleton<SocketIoConnectionHandler>();

builder.Services.AddHttpClient("RuntimeServer", client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("OpenHands:BaseUrl") ?? "http://127.0.0.1:3000";
    client.BaseAddress = new Uri(baseUrl);

    Console.WriteLine($"[RuntimeGateway] Registered HttpClient 'RuntimeServer' with BaseAddress = {client.BaseAddress}");
});
var app = builder.Build();

// Use CORS before sockets
app.UseCors("AllowFrontend");

// Swagger in dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/healthz", () => Results.Ok("healthy"));


// Enable WebSockets
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(240)
});

// Controllers
app.MapControllers();

// Socket.IO endpoints
app.Map("/socket.io", async context =>
{
    var handler = context.RequestServices.GetRequiredService<SocketIoConnectionHandler>();
    await handler.HandleAsync(context);
});

app.Map("/socket.io/{**_}", async context =>
{
    var handler = context.RequestServices.GetRequiredService<SocketIoConnectionHandler>();
    await handler.HandleAsync(context);
});

app.Map("/sockets/events/{conversationId}", async context =>
{
    var handler = context.RequestServices.GetRequiredService<SocketIoConnectionHandler>();
    await handler.HandleAsync(context);
});

app.Map("/sockets/events/{conversationId}/{**_}", async context =>
{
    var handler = context.RequestServices.GetRequiredService<SocketIoConnectionHandler>();
    await handler.HandleAsync(context);
});

app.Map("/runtime/{conversationId}/socket.io/{**_}", async context =>
{
    var handler = context.RequestServices.GetRequiredService<SocketIoConnectionHandler>();
    await handler.HandleAsync(context);
});

app.Map("/api/conversations/{conversationId}/sockets/events/{**rest}", async context =>
{
    Console.WriteLine("🔥 Gateway forwarding V1 WebSocket to API");

    if (context.WebSockets.IsWebSocketRequest)
    {
        var apiUrl = "ws://127.0.0.1:7247" + context.Request.Path + context.Request.QueryString;

        using var client = new ClientWebSocket();
        foreach (var header in context.Request.Headers)
            client.Options.SetRequestHeader(header.Key, header.Value);

        using var serverSocket = await context.WebSockets.AcceptWebSocketAsync();
        await client.ConnectAsync(new Uri(apiUrl), CancellationToken.None);

        var buffer = new byte[8192];

        // forward server → API
        var forwardToApi = Task.Run(async () =>
        {
            while (true)
            {
                var result = await serverSocket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.CloseStatus.HasValue)
                {
                    await client.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    break;
                }
                await client.SendAsync(buffer.AsMemory(0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
            }
        });

        // forward API → server
        var forwardToClient = Task.Run(async () =>
        {
            while (true)
            {
                var result = await client.ReceiveAsync(buffer, CancellationToken.None);
                if (result.CloseStatus.HasValue)
                {
                    await serverSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    break;
                }
                await serverSocket.SendAsync(buffer.AsMemory(0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
            }
        });

        await Task.WhenAll(forwardToApi, forwardToClient);
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Expected WebSocket request");
    }
});

app.Run();

static Uri BuildApiBaseAddress(OpenHandsOptions options)
{
    if (string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        throw new InvalidOperationException("OpenHands BaseUrl configuration is required.");
    }

    string baseUrl = options.BaseUrl.TrimEnd('/');
    string apiPrefix = options.ApiPrefix?.Trim('/') ?? string.Empty;

    string combined = string.IsNullOrEmpty(apiPrefix)
        ? $"{baseUrl}/"
        : $"{baseUrl}/{apiPrefix}/";

    return new Uri(combined, UriKind.Absolute);
}
