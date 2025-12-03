using System.Net;
using NetAI.RuntimeServer.Middleware;
using NetAI.RuntimeServer.Options;
using NetAI.RuntimeServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Load centralized ports configuration from solution root
var solutionRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, ".."));
var portsConfigPath = Path.Combine(solutionRoot, "backend-ports.json");
if (File.Exists(portsConfigPath))
{
    Console.WriteLine($"[RuntimeServer] Loading ports configuration from {portsConfigPath}");
    builder.Configuration.AddJsonFile(portsConfigPath, optional: true, reloadOnChange: true);
}
else
{
    Console.WriteLine("[RuntimeServer] backend-ports.json not found beside solution; falling back to local settings.");
    builder.Configuration.AddJsonFile("backend-ports.json", optional: true, reloadOnChange: true);
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Read from centralized config with fallback to appsettings
string host = builder.Configuration.GetValue<string>("BackendPorts:NetAI.RuntimeServer:Host") 
           ?? builder.Configuration.GetValue<string>("Host") 
           ?? "127.0.0.1";
int port = builder.Configuration.GetValue<int?>("BackendPorts:NetAI.RuntimeServer:Port") 
        ?? builder.Configuration.GetValue("Port", 7260);
bool useHttps = builder.Configuration.GetValue<bool?>("BackendPorts:NetAI.RuntimeServer:UseHttps") 
             ?? builder.Configuration.GetValue("UseHttps", false);

builder.WebHost.ConfigureKestrel(options =>
{
    if (!IPAddress.TryParse(host, out IPAddress? address))
        address = IPAddress.Any;

    if (useHttps)
    {
        options.Listen(address, port, listen => listen.UseHttps());
        Console.WriteLine($"RuntimeServer listening on https://{host}:{port}");
    }
    else
    {
        options.Listen(address, port);
        Console.WriteLine($"RuntimeServer listening on http://{host}:{port}");
    }
});

string[] allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? new[] { "http://localhost:3000", "http://localhost:3001" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<SessionApiKeyOptions>(options =>
{
    options.SessionApiKey = builder.Configuration["SessionApiKey"]
        ?? builder.Configuration["SESSION_API_KEY"];
});

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.Configure<AgentRuntimeOptions>(builder.Configuration.GetSection("AgentRuntime"));
builder.Services.Configure<WorkspaceOptions>(builder.Configuration.GetSection("Workspace"));
builder.Services.Configure<DirectoryZipperOptions>(builder.Configuration.GetSection("Zip"));
builder.Services.Configure<GitClientOptions>(builder.Configuration.GetSection("Git"));
builder.Services.PostConfigure<WorkspaceOptions>(options =>
{
    string candidateRoot = string.IsNullOrWhiteSpace(options.RootPath)
        ? solutionRoot
        : options.RootPath;

    // Resolve relative paths against the solution root to keep workspaces beside the repo
    string resolvedRoot = Path.GetFullPath(
        Path.IsPathRooted(candidateRoot)
            ? candidateRoot
            : Path.Combine(solutionRoot, candidateRoot));

    options.RootPath = resolvedRoot;
    Console.WriteLine($"[RuntimeServer] Workspace root resolved to {resolvedRoot}");
});
builder.Services.AddSingleton<IDirectoryZipper, DirectoryZipper>();
builder.Services.AddSingleton<IWorkspaceService, FileSystemWorkspaceService>();
builder.Services.AddSingleton<IGitClient, GitClient>();
builder.Services.AddSingleton<IFileEditService, FileEditService>();
builder.Services.AddSingleton<InMemoryConversationRuntime>();
builder.Services.AddSingleton<IAgentFrameworkClient, StubAgentFrameworkClient>();
builder.Services.AddSingleton<AgentFrameworkConversationRuntime>();
builder.Services.AddSingleton<IConversationRuntime>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentRuntimeOptions>>().Value;

    if (options.UseInMemoryStub)
    {
        return sp.GetRequiredService<InMemoryConversationRuntime>();
    }

    return sp.GetRequiredService<AgentFrameworkConversationRuntime>();
});

var app = builder.Build();

app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/healthz", () => Results.Ok("RuntimeServer healthy"));

//TODO WebSockets (optional future use)
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(240)
});

app.UseMiddleware<SessionApiKeyMiddleware>();

// Controller routing
app.MapControllers();

app.Run();