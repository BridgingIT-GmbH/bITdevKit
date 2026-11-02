namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Diagnostics;
using System.Security.Cryptography;
using BridgingIT.DevKit.Infrastructure.FileSystem;

internal static class SourceMap
{
    internal static object Capture(string repositoryRoot)
    {
        var targets = new (string Path, string[] Symbols)[]
        {
            ("src/Application.Storage/Blobs/BlobStorageBuilderContext.cs", ["RegisterClient("]),
            ("src/Application.Storage/Documents/DocumentStorageBuilderContext.cs", ["RegisterProvider<T>("]),
            ("src/Application.Storage/Blobs/IBlobStoreProvider.cs", ["Task<"]),
            ("src/Application.Storage/Documents/IDocumentStoreProvider.cs", ["Task<"]),
            ("src/Application.Storage/Blobs/Behaviors/RetryBlobStoreClientBehavior.cs", ["IsTransient", "ProviderError", "Timeout"]),
            ("src/Application.Storage/Documents/Behaviors/RetryDocumentStoreClientBehavior.cs", ["IsRetryable", "DocumentStoreProviderError"]),
            ("src/Application.Storage/Blobs/Retention/BlobRetentionBackgroundService.cs", ["SweepOnceAsync", "SweepExpiredAsync"]),
            ("src/Application.Storage/Documents/Retention/DocumentRetentionBackgroundService.cs", ["SweepOnceAsync", "SweepExpiredAsync"]),
            ("src/Application.Storage/Retention/StorageRetentionOptions.cs", ["public "]),
            ("tests/Application.UnitTests/Storage/Blobs/BlobStoreProviderContractTests.cs", ["class ", "Task "]),
            ("tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj", ["BlobStoreProviderContractTests"]),
            ("src/Application.Storage/Documents/Models/DocumentModels.cs", ["public "])
        };
        var entries = targets.Select(target => new
        {
            path = target.Path,
            exists = File.Exists(Path.Combine(repositoryRoot, target.Path)),
            matches = File.Exists(Path.Combine(repositoryRoot, target.Path))
                ? File.ReadLines(Path.Combine(repositoryRoot, target.Path)).Select((line, index) => new { line = index + 1, text = line.Trim() })
                    .Where(line => target.Symbols.Any(symbol => line.text.Contains(symbol, StringComparison.Ordinal))).ToArray() : []
        }).ToArray();

        return new
        {
            schemaVersion = 1,
            taskId = "TASK-001",
            status = entries.All(e => e.exists) ? "passed" : "failed",
            sourceHashes = Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src", "Infrastructure.FileSystem"), "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
                .Select(path => new { path = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'), sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) }).ToArray(),
            binarySha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(FileSystemStorageOptions).Assembly.Location))),
            head = File.Exists(Path.Combine(repositoryRoot, ".git")) || Directory.Exists(Path.Combine(repositoryRoot, ".git")) ? Git(repositoryRoot, "rev-parse", "HEAD") : "unavailable-copied-source",
            workingTreePaths = File.Exists(Path.Combine(repositoryRoot, ".git")) || Directory.Exists(Path.Combine(repositoryRoot, ".git")) ? Git(repositoryRoot, "status", "--porcelain") : "unavailable-copied-source",
            entries,
            documentContract = "No provider-neutral document base; EF-specific base cannot be reused."
        };
    }

    private static string Git(string root, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start);
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new IOException("Git inventory failed.");
        }

        return output.Trim();
    }
}