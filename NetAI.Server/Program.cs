using NetAI.Server.Options;
using NetAI.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

builder.Services
    .AddOptions<AgentRuntimeOptions>()
    .Bind(builder.Configuration.GetSection("AgentRuntime"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<WorkspaceOptions>()
    .Bind(builder.Configuration.GetSection("Workspace"));

builder.Services.AddSingleton<WorkspaceDirectoryProvider>();
builder.Services.AddSingleton<WasmRuntimeHost>();
builder.Services.AddSingleton<RuntimeEventStore>();
builder.Services.AddSingleton<ToolRegistry>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapGet("/alive", () => Results.Ok("alive"));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

public partial class Program { }
