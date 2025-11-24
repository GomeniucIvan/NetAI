using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using NetAI.Api;
using NetAI.Api.Application;
using NetAI.Api.Data;
using NetAI.Api.Data.Entities.Sandboxes;
using NetAI.Api.Services.Installation;
using NetAI.Api.Services.Sandboxes;
using NetAI.Api.Services.WebSockets;

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
string host = builder.Configuration.GetValue<string>("BackendPorts:NetAI.Api:Host") 
           ?? builder.Configuration.GetValue<string>("Host") 
           ?? "127.0.0.1";
int port = builder.Configuration.GetValue<int?>("BackendPorts:NetAI.Api:Port") 
        ?? builder.Configuration.GetValue("Port", 7247);
bool useHttps = builder.Configuration.GetValue<bool?>("BackendPorts:NetAI.Api:UseHttps") 
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
        Console.WriteLine($"Repository API running on https://{host}:{port}");
    }
    else
    {
        options.Listen(address, port);
        Console.WriteLine($"Repository API running on http://{host}:{port}");
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

builder.Services
    .AddControllers();

builder.Services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen(c =>
    {
        // Prevent duplicate schema name collisions (your current error)
        c.CustomSchemaIds(type => type.FullName);

        // Optional: include XML docs if you want descriptions in Swagger
        // var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        // var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        // c.IncludeXmlComments(xmlPath);

        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "NetAI",
            Version = "v1"
        });
    });

builder.Services.AddNetAiServices(builder.Configuration);

var app = builder.Build();

// Use CORS before sockets
app.UseCors("AllowFrontend");

var config = app.Configuration;

Console.WriteLine("== CONFIG DEBUG ==");
Console.WriteLine("Repository: " + config["Conversations:Repository:BaseUrl"]);
Console.WriteLine("RuntimeGateway: " + config["Conversations:RuntimeGateway:BaseUrl"]);
Console.WriteLine("Sandbox Orchestration: " + config["Sandboxes:Orchestration:ApiUrl"]);
Console.WriteLine("==================");

var sandboxOpts = app.Services.GetRequiredService<IOptions<SandboxOrchestrationOptions>>().Value;
Console.WriteLine($"SandboxOrchestrationOptions.ApiUrl = {sandboxOpts.ApiUrl}");

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

    if (!string.IsNullOrWhiteSpace(options.ConnectionString))
    {
        logger.LogInformation("Database connection found. Running migrations...");
        await DatabaseInitializer.InitializeAsync(app.Services);

        var db = scope.ServiceProvider.GetRequiredService<NetAiDbContext>();
        if (!db.SandboxSpecs.Any())
        {
            db.SandboxSpecs.Add(new SandboxSpecRecord
            {
                Id = "default",
                CommandJson = "[]",
                InitialEnvJson = "{}", 
                WorkingDir = "/workspace",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync();
            logger.LogInformation("Seeded default sandbox spec successfully.");
        }
    }
    else
    {
        logger.LogInformation("Skipping database initialization  no connection string configured yet.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(240)
});

app.Use(async (context, next) =>
{
    if (InstallationRequestMatcher.AllowsBypass(context))
    {
        await next().ConfigureAwait(false);
        return;
    }

    IApplicationContext applicationContext = context.RequestServices
        .GetRequiredService<IApplicationContext>();

    if (applicationContext.IsInstalled)
    {
        await next().ConfigureAwait(false);
        return;
    }

    if (InstallationRequestMatcher.ShouldRedirectToInstall(context))
    {
        context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
        context.Response.Headers.Location = "/install";
        return;
    }

    await Results.Json(
            new { error = "Installation is required before accessing this resource." },
            statusCode: StatusCodes.Status503ServiceUnavailable)
        .ExecuteAsync(context)
        .ConfigureAwait(false);
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", async (SystemStatusService statusSvc) =>
{
    var data = await statusSvc.GetSystemStatusAsync();
    var html = new StringBuilder();
    html.Append("<h2>NetAI System Status</h2>");
    html.Append("<table border='1' cellpadding='8'><tr><th>Service</th><th>URL</th><th>Status</th></tr>");

    foreach (var s in data.Results)
    {
        html.Append($"<tr><td>{s.Name}</td><td>{s.Url}</td><td>{s.Status}</td></tr>");
    }

    html.Append("</table>");
    html.Append($"<p>Last updated: {data.Timestamp:u}</p>");
    return Results.Content(html.ToString(), "text/html");
});

app.MapGet("/healthz", () => Results.Ok("Repository healthy"));

//app.MapPost("/api/install", (HttpContext context) =>
//{
//    Console.WriteLine("[MOCK] Received install request.");
//    return Results.Ok(new
//    {
//        status = "installed",
//        message = "Mock installation completed successfully."
//    });
//});

app.Map("/socket.io", static async context =>
{
    var handler = context.RequestServices.GetRequiredService<ConversationSocketIoHandler>();
    await handler.HandleAsync(context, context.RequestAborted).ConfigureAwait(false);
});

app.Map("/socket.io/{**_}", static async context =>
{
    var handler = context.RequestServices.GetRequiredService<ConversationSocketIoHandler>();
    await handler.HandleAsync(context, context.RequestAborted).ConfigureAwait(false);
});

app.Map("/sockets/events/{conversationId}", static async (HttpContext context, string conversationId) =>
{
    var handler = context.RequestServices.GetRequiredService<ConversationEventsWebSocketHandler>();
    await handler.HandleAsync(context, conversationId, context.RequestAborted).ConfigureAwait(false);
});

app.MapControllers();
app.Run();

public partial class Program;
