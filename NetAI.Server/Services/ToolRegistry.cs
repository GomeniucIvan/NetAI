using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Wasmtime;

namespace NetAI.Server.Services;

public interface IWorkspaceTool
{
    string Name { get; }
    int Execute(string workspacePath, string payload, out string result);
}

public sealed class ToolRegistry
{
    private readonly IReadOnlyList<IWorkspaceTool> _tools;
    private readonly Dictionary<string, IWorkspaceTool> _toolMap;

    public ToolRegistry()
    {
        _tools = new IWorkspaceTool[]
        {
            new FileTool(),
            new GitTool(),
            new PackagingTool(),
            new ListTool()
        };
        _toolMap = _tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    //public void Register(Linker linker, Store store)
    //{
    //    foreach (var tool in _tools)
    //    {
    //        linker.Define(
    //            "netai_tools",
    //            tool.Name,
    //            Function.FromCallback(
    //                store,
    //                (Caller caller, object[] args) =>
    //                {
    //                    var memory = caller.GetMemory("memory");
    //                    if (memory is null)
    //                        return new object[] { -1 };

    //                    var span = memory.GetSpan();

    //                    int workspacePtr = (int)args[0];
    //                    int workspaceLength = (int)args[1];
    //                    int payloadPtr = (int)args[2];
    //                    int payloadLength = (int)args[3];

    //                    if (workspacePtr < 0 || workspaceLength < 0 ||
    //                        payloadPtr < 0 || payloadLength < 0)
    //                        return new object[] { -2 };

    //                    var workspace = ReadUtf8(span, workspacePtr, workspaceLength);
    //                    var payload = ReadUtf8(span, payloadPtr, payloadLength);

    //                    var exitCode = tool.Execute(workspace, payload, out var result);

    //                    WriteUtf8(span, payloadPtr, payloadLength, result);

    //                    return new object[] { exitCode };
    //                },
    //                new[] { ValueKind.Int32, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32 },
    //                new[] { ValueKind.Int32 }
    //            )
    //        );
    //    }
    //}

    public void Register(Linker linker, Store store)
    {
        foreach (var tool in _tools)
        {
            linker.Define(
                "netai_tools",
                tool.Name,
                Function.FromCallback(
                    store,
                    (Caller caller, int workspacePtr, int workspaceLength, int payloadPtr, int payloadLength) =>
                    {
                        var memory = caller.GetMemory("memory");
                        if (memory is null)
                        {
                            return -1;
                        }

                        var span = memory.GetSpan();

                        if (workspacePtr < 0 || workspaceLength < 0 ||
                            payloadPtr < 0 || payloadLength < 0)
                        {
                            return -2;
                        }

                        var workspace = ReadUtf8(span, workspacePtr, workspaceLength);
                        var payload = ReadUtf8(span, payloadPtr, payloadLength);

                        var exitCode = tool.Execute(workspace, payload, out var result);

                        WriteUtf8(span, payloadPtr, payloadLength, result);

                        return exitCode;
                    })
            );
        }
    }


    private static string ReadUtf8(Span<byte> span, int offset, int length)
    {
        return Encoding.UTF8.GetString(span.Slice(offset, length));
    }

    private static void WriteUtf8(Span<byte> span, int offset, int length, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var count = Math.Min(length, bytes.Length);
        bytes.AsSpan(0, count).CopyTo(span.Slice(offset, count));
    }
}

internal sealed class ListTool : IWorkspaceTool
{
    public string Name => "list";

    public int Execute(string workspacePath, string payload, out string result)
    {
        result = string.Join(",", Directory.GetFiles(workspacePath));
        return 0;
    }
}

internal sealed class FileTool : IWorkspaceTool
{
    public string Name => "file";

    public int Execute(string workspacePath, string payload, out string result)
    {
        result = string.Empty;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return -1;
        }

        var parts = payload.Split(':', 2);
        if (parts.Length != 2)
        {
            return -2;
        }

        var command = parts[0];
        var path = Path.Combine(workspacePath, parts[1]);
        switch (command)
        {
            case "read":
                result = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
                return 0;
            case "write":
                File.WriteAllText(path, string.Empty);
                result = path;
                return 0;
            default:
                return -3;
        }
    }
}

internal sealed class GitTool : IWorkspaceTool
{
    public string Name => "git";

    public int Execute(string workspacePath, string payload, out string result)
    {
        result = string.Empty;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return -1;
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = payload
            }
        };

        process.Start();
        result = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        return process.ExitCode;
    }
}

internal sealed class PackagingTool : IWorkspaceTool
{
    public string Name => "package";

    public int Execute(string workspacePath, string payload, out string result)
    {
        result = string.Empty;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return -1;
        }

        var output = Path.Combine(workspacePath, payload);
        if (File.Exists(output))
        {
            File.Delete(output);
        }

        ZipFile.CreateFromDirectory(workspacePath, output);
        result = output;
        return 0;
    }
}
