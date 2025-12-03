using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NetAI.RuntimeServer.Models;

namespace NetAI.RuntimeServer.Services;

public interface IDirectoryZipper
{
    Task<RuntimeZipStreamResult> ZipDirectoryAsync(string sourceDirectory, CancellationToken cancellationToken);
}

public class DirectoryZipperOptions
{
    public string OutputFileName { get; set; } = "workspace.zip";

    public string ContentType { get; set; } = "application/zip";

    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Fastest;
}

public class DirectoryZipper : IDirectoryZipper
{
    private readonly DirectoryZipperOptions _options;

    public DirectoryZipper(IOptions<DirectoryZipperOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentException("Directory zipper options are required", nameof(options));
    }

    public Task<RuntimeZipStreamResult> ZipDirectoryAsync(string sourceDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            throw new ArgumentException("Source directory must be provided", nameof(sourceDirectory));
        }

        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Directory '{sourceDirectory}' does not exist.");
        }

        return Task.Run(() => CreateArchive(sourceDirectory, cancellationToken), cancellationToken);
    }

    private RuntimeZipStreamResult CreateArchive(string sourceDirectory, CancellationToken cancellationToken)
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relative = Path.GetRelativePath(sourceDirectory, file)
                    .Replace(Path.DirectorySeparatorChar, '/');

                ZipArchiveEntry entry = archive.CreateEntry(relative, _options.CompressionLevel);
                using Stream entryStream = entry.Open();
                using FileStream fileStream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                fileStream.CopyTo(entryStream);
            }
        }

        memoryStream.Position = 0;

        return new RuntimeZipStreamResult
        {
            Content = memoryStream,
            FileName = _options.OutputFileName,
            ContentType = _options.ContentType
        };
    }
}
