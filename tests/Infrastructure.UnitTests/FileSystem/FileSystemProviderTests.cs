namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using Application.Storage;
using Infrastructure.FileSystem;
using Infrastructure.FileSystem.Probes;
using Microsoft.Extensions.DependencyInjection;

public sealed class LocalFileSystemFactAttribute : FactAttribute
{
    public LocalFileSystemFactAttribute()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            this.Skip = "Filesystem providers require Windows or Linux.";
        }
    }
}

public sealed class LocalFileSystemTheoryAttribute : TheoryAttribute
{
    public LocalFileSystemTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            this.Skip = "Filesystem providers require Windows or Linux.";
        }
    }
}

public sealed class HostFileSystemFactAttribute : FactAttribute
{
    public HostFileSystemFactAttribute(string host)
    {
        if (host == "Windows" ? !OperatingSystem.IsWindows() : !OperatingSystem.IsLinux())
        {
            this.Skip = $"Requires {host}; skipped before fixture/filesystem access.";
        }
    }
}

internal sealed class OwnedFileSystemRoot : IDisposable
{
    private readonly string runId = Guid.NewGuid().ToString("N");

    internal string Path { get; }

    internal OwnedFileSystemRoot()
    {
        this.Path = ProbeWorkspace.Open(System.IO.Path.GetTempPath(), this.runId, "unit", true);
    }

    internal FileSystemStorageOptions Options => new()
    {
        RootPath = this.Path
    };

    public void Dispose()
    {
        ProbeWorkspace.Open(System.IO.Path.GetTempPath(), this.runId, "unit", false);

        foreach (var entry in Directory.EnumerateFileSystemEntries(this.Path, "*", SearchOption.AllDirectories))
        {
            ProbeWorkspace.RejectLinks(entry);
        }

        Directory.Delete(this.Path, true);
    }
}

public class FileSystemBlobContractTests : Application.UnitTests.Storage.BlobStoreProviderContractTests, IDisposable
{
    private readonly OwnedFileSystemRoot root = new();

    protected override string ProviderName => "FileSystem";

    protected virtual FileSystemStorageDurability Durability => FileSystemStorageDurability.Durable;

    protected override IBlobStoreProvider CreateProvider(BlobStoreOptions options = null)
    {
        var filesystem = this.root.Options;
        filesystem.Durability = this.Durability;

        return new FileSystemBlobStoreProvider(filesystem, options);
    }

    public void Dispose()
    {
        this.root.Dispose();
    }
}

public sealed class FileSystemBufferedBlobContractTests : FileSystemBlobContractTests
{
    protected override FileSystemStorageDurability Durability => FileSystemStorageDurability.Buffered;
}

public sealed class FileSystemProviderTests
{
    [LocalFileSystemFact]
    public async Task StoreOptions_MutatingOriginalAndPublicCopy_DoesNotReconfigureActiveProvider()
    {
        using var root = new OwnedFileSystemRoot();
        var options = root.Options;
        var sut = new FileSystemBlobStoreProvider(options);
        var captured = sut.Store.Options;
        captured.RootPath = Path.Combine(root.Path, "copy-must-not-create");
        captured.AccessKind = FileSystemStorageAccessKind.Smb;
        captured.Durability = FileSystemStorageDurability.Buffered;
        captured.MaxQueryMetadataEntries = 1;
        options.RootPath = Path.Combine(root.Path, "original-must-not-create");
        options.AccessKind = FileSystemStorageAccessKind.Smb;
        var uploaded = await sut.UploadAsync(new() { Key = new("catalog", "snapshot"), Content = new MemoryStream([1, 2]) });
        uploaded.IsSuccess.ShouldBeTrue();
        sut.Store.Options.RootPath.ShouldBe(root.Path);
        sut.Store.Options.AccessKind.ShouldBe(FileSystemStorageAccessKind.Local);
        sut.Store.Options.Durability.ShouldBe(FileSystemStorageDurability.Durable);
        sut.Store.MaxQueryMetadataEntries.ShouldBe(100000);
        Directory.Exists(captured.RootPath).ShouldBeFalse();
        Directory.Exists(options.RootPath).ShouldBeFalse();
        (await new FileSystemBlobStoreProvider(root.Options).GetPropertiesAsync(new("catalog", "snapshot"))).IsSuccess.ShouldBeTrue();
    }

    [LocalFileSystemFact]
    public void ManifestJson_PublicSettings_RejectMutationAndPreservePropertyBagScalarTypes()
    {
        var settings = FileSystemStore.Json;
        settings.IsReadOnly.ShouldBeTrue();
        Assert.Throws<InvalidOperationException>(() => settings.MaxDepth = 1);
        Assert.Throws<InvalidOperationException>(() => settings.Converters.Clear());
        var properties = new PropertyBag
        {
            ["count"] = 12,
            ["name"] = "catalog",
            ["enabled"] = true
        };
        var json = System.Text.Json.JsonSerializer.Serialize(properties, settings);
        var restored = System.Text.Json.JsonSerializer.Deserialize<PropertyBag>(json, settings);
        restored["count"].ShouldBeOfType<int>().ShouldBe(12);
        restored["name"].ShouldBeOfType<string>().ShouldBe("catalog");
        restored["enabled"].ShouldBeOfType<bool>().ShouldBeTrue();
    }

    [LocalFileSystemFact]
    public void IdentityHash_CanonicalBigEndianLengths_MatchesFormatVectorAndSeparatesSegmentBoundaries()
    {
        FileSystemStore.Hash(["A", "BC"]).ShouldBe("3aa71345508f5eaf0cfe4bf8ba3e5f49f3b1b17a431a3da38a9576a06ede94eb");
        FileSystemStore.Hash(["AB", "C"]).ShouldNotBe(FileSystemStore.Hash(["A", "BC"]));
    }

    [LocalFileSystemFact]
    public async Task MalformedUnicodeIdentity_IsRejectedBeforeRootAccess_AndCannotAliasAValidReplacementCharacter()
    {
        using var root = new OwnedFileSystemRoot();
        var blobs = new FileSystemBlobStoreProvider(root.Options);
        (await blobs.UploadAsync(new() { Key = new("catalog", "\uD800"), Content = new MemoryStream([1]) })).HasError<BlobStoreValidationError>().ShouldBeTrue();
        var documents = new FileSystemDocumentStoreProvider(root.Options);
        (await documents.UpsertAsync(DocumentTypeIdentity.For<TestDocument>(), new() { Key = new("partition", "\uDC00"), Content = [1] })).HasError<ValidationError>().ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(root.Path).Select(Path.GetFileName).ShouldBe(new[] { "run.json" });
        var valid = new BlobKey("catalog", "\uFFFD");
        (await blobs.UploadAsync(new() { Key = valid, Content = new MemoryStream([2]) })).IsSuccess.ShouldBeTrue();
        var download = await blobs.DownloadAsync(valid);
        await using var content = download.Value.Content;
        content.ReadByte().ShouldBe(2);
    }

    [LocalFileSystemFact]
    public void QualificationProfile_LocalTempRoot_IsRejectedAsNetworkEvidence()
    {
        using var root = new OwnedFileSystemRoot();
        var profile = new QualificationProfile("not-a-share", OperatingSystem.IsWindows() ? "Windows" : "Linux", "Smb", root.Path,
            "client", "server", "SMB 3.x", "OS-managed", "first-host", "second-host", new());
        var exception = Assert.Throws<InvalidDataException>(() => RemoteFileSystemProbe.Validate(profile));
        exception.Message.Contains("local", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(root.Path).Select(Path.GetFileName).ShouldBe(new[] { "run.json" });
    }

    [LocalFileSystemFact]
    public async Task DefaultDurability_RestartMetadataDelete_PersistsAndDoesNotCreateAbsentRecordDirectories()
    {
        using var root = new OwnedFileSystemRoot();
        var path = Path.Combine(root.Path, "not-created");
        var sut = new FileSystemBlobStoreProvider(new() { RootPath = path });
        var result = await sut.ExistsAsync(new("catalog", "missing"));
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
        Directory.Exists(Path.Combine(path, "blobs")).ShouldBeFalse();
        var key = new BlobKey("catalog", "one");
        var uploaded = await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([1, 2, 3]) });
        uploaded.IsSuccess.ShouldBeTrue();
        var restarted = new FileSystemBlobStoreProvider(new() { RootPath = path });
        (await restarted.UpdatePropertiesAsync(new() { Key = key, IfMatchETag = uploaded.Value.ETag, Properties = new() { ["source"] = "durable" } })).IsSuccess.ShouldBeTrue();
        (await restarted.DeleteAsync(key)).IsSuccess.ShouldBeTrue();
        (await new FileSystemBlobStoreProvider(new() { RootPath = path }).ExistsAsync(key)).Value.ShouldBeFalse();
        Directory.EnumerateFiles(path, "payload-*.bin", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [LocalFileSystemFact]
    public async Task SmbProtocol_LocalDisk_UsesPersistentOwnerWithoutClaimingNetworkQualification()
    {
        await ExerciseProtocolAsync(FileSystemStorageAccessKind.Smb);
    }

    [HostFileSystemFact("Linux")]
    public async Task LinuxNfsProtocol_LocalDisk_UsesPersistentOwnerWithoutClaimingNetworkQualification()
    {
        await ExerciseProtocolAsync(FileSystemStorageAccessKind.Nfs);
    }

    [HostFileSystemFact("Windows")]
    public void WindowsNfsConfiguration_RejectsWithoutFilesystemAccess()
    {
        new FileSystemStorageOptions
        {
            RootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            AccessKind = FileSystemStorageAccessKind.Nfs
        }.Validate().IsFailure.ShouldBeTrue();
    }

    private static async Task ExerciseProtocolAsync(FileSystemStorageAccessKind access)
    {
        using var root = new OwnedFileSystemRoot();
        var options = root.Options;
        options.AccessKind = access;
        var sut = new FileSystemBlobStoreProvider(options);
        (await sut.ExistsAsync(new("catalog", "missing"))).IsSuccess.ShouldBeTrue();
        (await sut.UploadAsync(new() { Key = new("catalog", "record"), Content = new MemoryStream([1]) })).IsSuccess.ShouldBeTrue();
        Directory.EnumerateFiles(root.Path, "authority", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [LocalFileSystemFact]
    public async Task DocumentMutation_RestartAndPhysicalExpiration_PreservesConditionsAndMetadata()
    {
        using var root = new OwnedFileSystemRoot();
        var sut = new FileSystemDocumentStoreProvider(root.Options);
        var type = DocumentTypeIdentity.For<TestDocument>();
        var key = new DocumentKey("part/é", "ROW\\文件");
        var expiry = DateTimeOffset.UtcNow.AddMinutes(-1);
        var created = await sut.UpsertAsync(type, new() { Key = key, Content = [1, 2, 3], ExpiresAt = expiry,
            ContentHash = "logical", StoredContentHash = "stored", TransformMetadata = new() { ["format"] = "test" } });
        created.IsSuccess.ShouldBeTrue();
        var restarted = new FileSystemDocumentStoreProvider(root.Options);
        (await restarted.ExistsAsync(type, key, DateTimeOffset.UtcNow)).Value.ShouldBeFalse();
        (await restarted.UpsertAsync(type, new() { Key = key, Options = new() { CreateOnly = true } })).HasError<DocumentStoreConflictError>().ShouldBeTrue();
        var visible = await restarted.GetAsync(type, key, expiry.AddSeconds(-1));
        visible.IsSuccess.ShouldBeTrue();
        visible.Value.Content.ShouldBe(new byte[] { 1, 2, 3 });
        visible.Value.StoredContentHash.ShouldBe("stored");
        visible.Value.TransformMetadata.Get<string>("format").ShouldBe("test");
        visible.Value.Content[0] = 99;
        (await restarted.GetAsync(type, key, expiry.AddSeconds(-1))).Value.Content[0].ShouldBe((byte)1);
        var updated = await restarted.UpdatePropertiesAsync(type, new(key) { IfMatchETag = created.Value.ETag }, null, false);
        updated.IsSuccess.ShouldBeTrue();
        (await restarted.ExistsAsync(type, key, DateTimeOffset.UtcNow)).Value.ShouldBeTrue();
        (await restarted.DeleteAsync(type, key, new() { IfMatchETag = created.Value.ETag })).HasError<DocumentStoreConflictError>().ShouldBeTrue();
    }

    [LocalFileSystemFact]
    public async Task OpenReader_DeleteAndReplace_RetainsOriginalGeneration()
    {
        using var root = new OwnedFileSystemRoot();
        var sut = new FileSystemBlobStoreProvider(root.Options);
        var key = new BlobKey("catalog", "reader");
        (await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([1, 2, 3]) })).IsSuccess.ShouldBeTrue();
        var download = await sut.DownloadAsync(key);
        await using var reader = download.Value.Content;
        (await sut.DeleteAsync(key)).IsSuccess.ShouldBeTrue();
        (await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([9]) })).IsSuccess.ShouldBeTrue();
        using var output = new MemoryStream();
        await reader.CopyToAsync(output);
        output.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
    }

    [LocalFileSystemFact]
    public void QualificationConfiguration_MalformedAndCredentialBearingInputs_AreRejectedWithoutEcho()
    {
        using var root = new OwnedFileSystemRoot();
        var path = Path.Combine(root.Path, "input.json");

        foreach (var input in new[]
        {
            "{}",
            "{\"schemaVersion\":2,\"profiles\":[]}",
            "{\"password\":\"synthetic-sensitive-value\"}"
        }
        )
        {
            File.WriteAllText(path, input);
            var exception = Assert.Throws<InvalidDataException>(() => QualificationConfiguration.LoadConfiguration(path));
            exception.Message.ShouldNotContain("synthetic-sensitive-value");
        }
    }

    [LocalFileSystemFact]
    public async Task NamedRegistrations_ExistingRetentionWorkers_DeleteOnlyExpiredRecords()
    {
        using var root = new OwnedFileSystemRoot();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<Microsoft.Extensions.Hosting.IHostApplicationLifetime>());
        services.AddBlobStorage().WithFileSystemClient("local", root.Path);
        services.AddDocumentStorage().WithFileSystemClient<TestDocument>(root.Path);
        using var container = services.BuildServiceProvider();
        var blob = container.GetRequiredService<IBlobStoreClientFactory>().CreateClient("local");
        var document = container.GetRequiredService<IDocumentStoreClient<TestDocument>>();
        var key = new BlobKey("catalog", "expired");
        var documentKey = new DocumentKey("catalog", "expired");
        (await blob.UploadAsync(new() { Key = key, Content = new MemoryStream([1]), ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) })).IsSuccess.ShouldBeTrue();
        (await document.UpsertAsync(documentKey, new(), new() { Expiration = ExpirationChange.At(DateTimeOffset.UtcNow.AddDays(-1)) })).IsSuccess.ShouldBeTrue();
        var blobSweep = await container.GetRequiredService<BlobRetentionBackgroundService>().SweepOnceAsync();
        var documentSweep = await container.GetRequiredService<DocumentRetentionBackgroundService>().SweepOnceAsync();
        blobSweep.IsSuccess.ShouldBeTrue();
        blobSweep.Value.DeletedCount.ShouldBe(1);
        documentSweep.IsSuccess.ShouldBeTrue();
        documentSweep.Value.Sum(s => s.DeletedCount).ShouldBe(1);
        (await blob.ExistsAsync(key)).Value.ShouldBeFalse();
        (await document.ExistsAsync(documentKey)).Value.ShouldBeFalse();
    }

    [LocalFileSystemFact]
    public async Task DocumentPaging_OrdinalKeysAndFixedCutoff_TraversesUnchangedDatasetOnce()
    {
        using var root = new OwnedFileSystemRoot();
        var sut = new FileSystemDocumentStoreProvider(root.Options);
        var type = DocumentTypeIdentity.For<TestDocument>();
        var names = new[]
        {
            "a",
            "A",
            "é",
            "文件",
            "a/b",
            "a\\b"
        }.Order(StringComparer.Ordinal).ToArray();

        foreach (var name in names)
        {
            (await sut.UpsertAsync(type, new() { Key = new("part", name), Content = [1] })).IsSuccess.ShouldBeTrue();
        }

        var cutoff = DateTimeOffset.UtcNow;
        var actual = new List<string>();
        string token = null;
        do
        {
            var page = await sut.ListPageAsync(type, new() { DocumentKey = new("part", ""), Filter = DocumentKeyFilter.RowKeyPrefixMatch, Take = 2, ContinuationToken = token }, cutoff);
            page.IsSuccess.ShouldBeTrue();
            actual.AddRange(page.Value.Items.Select(k => k.RowKey));
            token = page.Value.ContinuationToken;
        }
        while (token is not null);
        actual.ShouldBe(names);
    }

    [LocalFileSystemFact]
    public async Task Retention_SmallPersistedDiscoveryBudget_EventuallyWrapsAndPreservesExtendedExpiration()
    {
        using var root = new OwnedFileSystemRoot();
        var options = root.Options;
        options.MaxRetentionMetadataEntries = 8;
        var sut = new FileSystemBlobStoreProvider(options);

        foreach (var name in Enumerable.Range(0, 40).Select(i => "item-" + i))
        {
            (await sut.UploadAsync(new() { Key = new("catalog", name), Content = new MemoryStream([1]), ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) })).IsSuccess.ShouldBeTrue();
        }

        (await sut.UpdatePropertiesAsync(new() { Key = new("catalog", "item-0"), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) })).IsSuccess.ShouldBeTrue();
        var deleted = new HashSet<BlobKey>();

        for (var iteration = 0; iteration < 5000 && deleted.Count < 39; iteration++)
        {
            var restarted = new FileSystemBlobStoreProvider(options);
            var completed = await restarted.SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow, BatchSize = 3, MaxBatches = 1 });
            completed.IsSuccess.ShouldBeTrue();

            foreach (var key in completed.Value.DeletedKeys)
            {
                deleted.Add(key);
            }
        }

        deleted.Count.ShouldBe(39);
        (await sut.ExistsAsync(new("catalog", "item-0"))).Value.ShouldBeTrue();
        Directory.EnumerateFiles(root.Path, "payload-*.bin", SearchOption.AllDirectories).Count().ShouldBe(1);
    }

    public sealed class TestDocument;
}