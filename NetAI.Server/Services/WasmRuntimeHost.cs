using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NetAI.Server.Models;
using Wasmtime;

namespace NetAI.Server.Services;

public sealed class WasmRuntimeHost
{
    private readonly ToolRegistry _tools;

    public WasmRuntimeHost(ToolRegistry tools)
    {
        _tools = tools;
    }

    public async Task<WasmExecutionResult> RunAsync(StartRuntimeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(request.ModulePath))
        {
            throw new FileNotFoundException($"Module not found at '{request.ModulePath}'.", request.ModulePath);
        }

        using var engine = new Engine(new Config());
        using var store = new Store(engine);

        var stdout = new MemoryStream();
        var stderr = new MemoryStream();

        var stdoutPath = Path.GetTempFileName();
        var stderrPath = Path.GetTempFileName();

        var wasi = new WasiConfiguration()
            .WithInheritedEnvironment()
            .WithStandardOutput(stdoutPath)
            .WithStandardError(stderrPath)
            .WithArgs(request.Arguments.ToArray());

        if (!string.IsNullOrWhiteSpace(request.WorkspacePath))
        {
            var workspace = Path.GetFullPath(request.WorkspacePath);
            if (Directory.Exists(workspace))
            {
                wasi = wasi.WithPreopenedDirectory(workspace, "/workspace");
            }
        }

        store.SetWasiConfiguration(wasi);

        using var linker = new Linker(engine);
        linker.DefineWasi();

        _tools.Register(linker, store);

        var module = Module.FromFile(engine, request.ModulePath);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (request.TimeoutSeconds > 0)
        {
            cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));
        }

        var startTime = Stopwatch.GetTimestamp();

        try
        {
            var instance = linker.Instantiate(store, module);
            var start = instance.GetAction("_start") ?? instance.GetAction("main");
            if (start is null)
            {
                throw new InvalidOperationException("Module does not expose an entry point.");
            }

            await Task.Run(() => start(), cts.Token);

            var duration = Stopwatch.GetElapsedTime(startTime);
            return new WasmExecutionResult
            {
                ExitCode = 0,
                Stdout = Encoding.UTF8.GetString(stdout.ToArray()),
                Stderr = Encoding.UTF8.GetString(stderr.ToArray()),
                Duration = duration
            };
        }
        catch (OperationCanceledException)
        {
            return new WasmExecutionResult
            {
                ExitCode = -1,
                Stdout = Encoding.UTF8.GetString(stdout.ToArray()),
                Stderr = Encoding.UTF8.GetString(stderr.ToArray()),
                Duration = Stopwatch.GetElapsedTime(startTime)
            };
        }
    }
}
