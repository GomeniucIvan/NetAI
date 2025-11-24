using System;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetAI.SandboxOrchestration.Options;
using NetAI.SandboxOrchestration.Services;
using System.Linq;
using System.Net.Http.Headers;

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

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Read from centralized config with fallback to appsettings
string host = builder.Configuration.GetValue<string>("BackendPorts:NetAI.SandboxOrchestration:Host") 
           ?? builder.Configuration.GetValue<string>("Host") 
           ?? "127.0.0.1";
int port = builder.Configuration.GetValue<int?>("BackendPorts:NetAI.SandboxOrchestration:Port") 
        ?? builder.Configuration.GetValue("Port", 7251);
bool useHttps = builder.Configuration.GetValue<bool?>("BackendPorts:NetAI.SandboxOrchestration:UseHttps") 
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
        Console.WriteLine($"SandboxOrchestration listening on https://{host}:{port}");
    }
    else
    {
        options.Listen(address, port);
        Console.WriteLine($"SandboxOrchestration listening on http://{host}:{port}");
    }
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services
    .AddOptions<OpenHandsOptions>()
    .Bind(builder.Configuration.GetSection(OpenHandsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<IOpenHandsClient, OpenHandsClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OpenHandsOptions>>().Value;
    client.BaseAddress = new Uri(options.ApiBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);

    if (!string.IsNullOrWhiteSpace(options.ApiKey))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }

    if (!client.DefaultRequestHeaders.Accept.Any())
    {
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
});

builder.Services.AddScoped<SandboxLifecycleService>();

var app = builder.Build();
app.MapGet("/healthz", () => Results.Ok("healthy"));


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
