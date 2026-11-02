namespace BridgingIT.DevKit.Application.Benchmarks;

using BenchmarkDotNet.Attributes;
using Application.Storage;
using BridgingIT.DevKit.Infrastructure.FileSystem;
using System.Security.Cryptography;

/// <summary>Verified local filesystem operation measurements. Deployment acceptance requires the full load and qualification matrix.</summary>
/// <example><code>BenchmarkSwitcher.FromAssembly(typeof(FileSystemStorageBenchmarks).Assembly).Run(args);</code></example>
[MemoryDiagnoser]
public class FileSystemStorageBenchmarks
{
    private string root;
    private string identity;
    private FileSystemBlobStoreProvider provider;
    private byte[] fixture;
    private byte[] expectedHash;
    private FileSystemDocumentStoreProvider documents;
    private readonly DocumentKey documentKey = new("benchmark", "record");
    private readonly DocumentTypeIdentity documentType = DocumentTypeIdentity.For<BenchmarkDocument>();
    private readonly BlobKey key = new("benchmark", "record");

    /// <summary>Gets or sets the fixture size in bytes.</summary>
    /// <example><code>benchmark.PayloadBytes = 65536;</code></example>
    [Params(4096, 65536)]
    public int PayloadBytes { get; set; }

    /// <summary>Gets or sets the acknowledged persistence mode measured by this fixture.</summary>
    /// <example><code>benchmark.Durability = FileSystemStorageDurability.Durable;</code></example>
    [Params(FileSystemStorageDurability.Durable, FileSystemStorageDurability.Buffered)]
    public FileSystemStorageDurability Durability { get; set; }

    /// <summary>Creates a unique local fixture outside the timed region.</summary>
    /// <example><code>await benchmark.SetupAsync();</code></example>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Windows/Linux local scenarios only.");
        }

        this.identity = Guid.NewGuid().ToString("N");
        this.root = Path.Combine(Path.GetTempPath(), "bdk-filesystem-benchmarks", this.identity);
        Directory.CreateDirectory(this.root);
        File.WriteAllText(Path.Combine(this.root, "benchmark-owner"), this.identity);
        this.fixture = new byte[this.PayloadBytes];
        RandomNumberGenerator.Fill(this.fixture);
        this.expectedHash = SHA256.HashData(this.fixture);
        var options = new FileSystemStorageOptions
        {
            RootPath = this.root,
            Durability = this.Durability
        };
        this.provider = new(options);
        this.documents = new(options);
        using var source = new MemoryStream(this.fixture, false);
        var result = await this.provider.UploadAsync(new() { Key = this.key, Content = source });

        if (result.IsFailure)
        {
            throw new InvalidOperationException("Fixture creation failed.");
        }

        var document = await this.documents.UpsertAsync(this.documentType, new() { Key = this.documentKey, Content = this.fixture,
            ContentHash = "sha256:" + Convert.ToHexStringLower(this.expectedHash), StoredContentHash = "sha256:" + Convert.ToHexStringLower(this.expectedHash) });

        if (document.IsFailure)
        {
            throw new InvalidOperationException("Document fixture failed.");
        }

        Console.WriteLine($"OS={(OperatingSystem.IsWindows() ? "Windows" : "Linux")}; access=Local; durability={this.Durability}; cache=uncontrolled; payload={this.PayloadBytes}");
    }

    /// <summary>Consumes and verifies every downloaded byte inside the timed operation.</summary>
    /// <example><code>await benchmark.VerifiedDownloadAsync();</code></example>
    [Benchmark]
    public async Task VerifiedDownloadAsync()
    {
        var result = await this.provider.DownloadAsync(this.key);

        if (result.IsFailure)
        {
            throw new InvalidOperationException("Download failed.");
        }

        await using var content = result.Value.Content;
        var hash = await SHA256.HashDataAsync(content);

        if (!hash.AsSpan().SequenceEqual(this.expectedHash))
        {
            throw new InvalidDataException("Downloaded bytes differ.");
        }
    }

    /// <summary>Reads metadata without reading payload files.</summary>
    /// <example><code>await benchmark.MetadataAsync();</code></example>
    [Benchmark]
    public async Task MetadataAsync()
    {
        var result = await this.provider.GetPropertiesAsync(this.key);

        if (result.IsFailure || result.Value.Length != this.PayloadBytes)
        {
            throw new InvalidOperationException("Metadata verification failed.");
        }
    }

    /// <summary>Overwrites and verifies a blob through the production persistence path.</summary>
    /// <example><code>await benchmark.BlobWriteAsync();</code></example>
    [Benchmark]
    public async Task BlobWriteAsync()
    {
        using var source = new MemoryStream(this.fixture, false);
        var result = await this.provider.UploadAsync(new() { Key = this.key, Content = source });

        if (result.IsFailure || result.Value.ContentHash != "sha256:" + Convert.ToHexStringLower(this.expectedHash))
        {
            throw new InvalidDataException("Write verification failed.");
        }
    }

    /// <summary>Publishes metadata without reading unchanged blob bytes.</summary>
    /// <example><code>await benchmark.BlobMetadataWriteAsync();</code></example>
    [Benchmark]
    public async Task BlobMetadataWriteAsync()
    {
        var result = await this.provider.UpdatePropertiesAsync(new() { Key = this.key });

        if (result.IsFailure || result.Value.Length != this.PayloadBytes)
        {
            throw new InvalidDataException("Metadata write failed.");
        }
    }

    /// <summary>Reads and verifies persisted document bytes.</summary>
    /// <example><code>await benchmark.DocumentReadAsync();</code></example>
    [Benchmark]
    public async Task DocumentReadAsync()
    {
        var result = await this.documents.GetAsync(this.documentType, this.documentKey, DateTimeOffset.UtcNow);

        if (result.IsFailure || !result.Value.Content.AsSpan().SequenceEqual(this.fixture))
        {
            throw new InvalidDataException("Document verification failed.");
        }
    }

    /// <summary>Overwrites one serialized document through the provider.</summary>
    /// <example><code>await benchmark.DocumentWriteAsync();</code></example>
    [Benchmark]
    public async Task DocumentWriteAsync()
    {
        var result = await this.documents.UpsertAsync(this.documentType, new() { Key = this.documentKey, Content = this.fixture,
            ContentHash = "sha256:" + Convert.ToHexStringLower(this.expectedHash), StoredContentHash = "sha256:" + Convert.ToHexStringLower(this.expectedHash) });

        if (result.IsFailure)
        {
            throw new InvalidOperationException("Document write failed.");
        }
    }

    /// <summary>Lists document keys without payload materialization.</summary>
    /// <example><code>await benchmark.DocumentKeysAsync();</code></example>
    [Benchmark]
    public async Task DocumentKeysAsync()
    {
        var result = await this.documents.ListPageAsync(this.documentType, new() { DocumentKey = this.documentKey }, DateTimeOffset.UtcNow);

        if (result.IsFailure || result.Value.Items.Count != 1)
        {
            throw new InvalidDataException("Key listing failed.");
        }
    }

    /// <summary>Creates a due record and requires physical retention to delete its committed reference.</summary>
    /// <example><code>await benchmark.RetentionAsync();</code></example>
    [Benchmark]
    public async Task RetentionAsync()
    {
        var key = new BlobKey("retention", "due");
        using var source = new MemoryStream(this.fixture, false);
        var write = await this.provider.UploadAsync(new() { Key = key, Content = source, ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) });
        var sweep = await this.provider.SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow });

        if (write.IsFailure || sweep.IsFailure || !sweep.Value.DeletedKeys.Contains(key))
        {
            throw new InvalidDataException("Retention did not execute the required deletion.");
        }
    }

    /// <summary>Deletes only this run's verified owned local directory.</summary>
    /// <example><code>benchmark.Cleanup();</code></example>
    [GlobalCleanup]
    public void Cleanup()
    {
        if (this.root != Path.Combine(Path.GetTempPath(), "bdk-filesystem-benchmarks", this.identity)
            || File.ReadAllText(Path.Combine(this.root, "benchmark-owner")) != this.identity)
        {
            throw new InvalidDataException("Ownership mismatch.");
        }

        FileSystemStore.RejectLinks(this.root);

        foreach (var path in Directory.EnumerateFileSystemEntries(this.root, "*", SearchOption.AllDirectories))
        {
            FileSystemStore.RejectLinks(path);
        }

        Directory.Delete(this.root, true);
    }

    /// <summary>Defines a fixture namespace for raw serialized document benchmarks.</summary>
    /// <example><code>var type = DocumentTypeIdentity.For&lt;BenchmarkDocument&gt;();</code></example>
    public sealed class BenchmarkDocument;
}