namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using Application.Storage;
using Infrastructure.FileSystem;
using System.Security.Cryptography;

public sealed class FileSystemIntegrationBehaviorTests
{
    [LocalFileSystemFact]
    public async Task TypedTransforms_RestartCopyMoveAndMultiKeyHelpers_RoundTripCompleteMetadata()
    {
        using var root = new OwnedFileSystemRoot();
        var provider = new FileSystemDocumentStoreProvider(root.Options);
        var keyProvider = new DictionaryEncryptionKeyProvider("test", new Dictionary<string, byte[]> { ["test"] = RandomNumberGenerator.GetBytes(32) });
        IDocumentPayloadTransform[] transforms = [new CompressionDocumentPayloadTransform(), new EncryptionDocumentPayloadTransform(keyProvider)];
        IDocumentStoreClient<Model> sut = new DocumentStoreClient<Model>(provider, transforms: transforms);
        var original = new DocumentKey("partition", "original");
        var value = new Model
        {
            Name = new string ('A', 4096)
        };
        (await sut.UpsertAsync(original, value, new() { Properties = new() { ["amount"] = 12.5m, ["active"] = true } })).IsSuccess.ShouldBeTrue();
        var restarted = new DocumentStoreClient<Model>(new FileSystemDocumentStoreProvider(root.Options), transforms: transforms);
        var read = await restarted.GetAsync(original);
        read.IsSuccess.ShouldBeTrue();
        read.Value.Value.Name.ShouldBe(value.Name);
        read.Value.Properties.Get<decimal>("amount").ShouldBe(12.5m);
        var stored = await provider.GetAsync(DocumentTypeIdentity.For<Model>(), original, DateTimeOffset.UtcNow);
        stored.Value.ContentHash.ShouldNotBe(stored.Value.StoredContentHash);
        stored.Value.TransformMetadata.Get<string>("bdk_transform_envelope").ShouldNotBeNullOrWhiteSpace();
        (await sut.CopyAsync(original, restarted, new("partition", "copy"))).IsSuccess.ShouldBeTrue();
        (await sut.MoveAsync(new("partition", "copy"), restarted, new("partition", "moved"))).IsSuccess.ShouldBeTrue();
        var many = await restarted.GetManyAsync([original, new("partition", "moved"), new("partition", "copy")]);
        many.IsSuccess.ShouldBeTrue();
        many.Value[0].Value.Value.Name.ShouldBe(value.Name);
        many.Value[1].Value.Value.Name.ShouldBe(value.Name);
        many.Value[2].HasError<DocumentStoreNotFoundError>().ShouldBeTrue();
    }

    [LocalFileSystemFact]
    public async Task Retention_BusyKeyAndExpirationClearedAfterDiscovery_RechecksCurrentStateAndWraps()
    {
        using var root = new OwnedFileSystemRoot();
        var sut = new FileSystemBlobStoreProvider(root.Options);

        foreach (var name in new[]
        {
            "busy",
            "clear",
            "due"
        }
        )
        {
            (await sut.UploadAsync(new() { Key = new("catalog", name), Content = new MemoryStream([1]), ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) })).IsSuccess.ShouldBeTrue();
        }

        var busyPath = sut.Store.Resolve(["blobs", "catalog", "busy"], false);
        using var busy = await sut.Store.AcquireAsync(busyPath, false, default);
        var discovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        sut.Store.Fault = point =>
        {
            if (point != "retention-discovered")
            {
                return;
            }

            discovered.TrySetResult();

            if (!release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Test barrier deadline.");
            }
        };
        var sweeping = Task.Run(() => sut.SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow }));
        await discovered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var concurrent = new FileSystemBlobStoreProvider(root.Options);
        (await concurrent.UpdatePropertiesAsync(new() { Key = new("catalog", "clear"), ExpiresAt = null })).IsSuccess.ShouldBeTrue();
        release.Set();
        var result = await sweeping;
        result.IsSuccess.ShouldBeTrue();
        result.Value.DeletedKeys.ShouldBe(new[] { new BlobKey("catalog", "due") });
        result.Value.SkippedCount.ShouldBe(1);
        busy.Dispose();
        sut.Store.Fault = null;
        var next = await sut.SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow });
        next.Value.DeletedKeys.ShouldBe(new[] { new BlobKey("catalog", "busy") });
        (await sut.ExistsAsync(new("catalog", "clear"))).Value.ShouldBeTrue();
    }

    [LocalFileSystemFact]
    public async Task ThrowingAndCanceledSources_PreserveCommittedBytes_AndDoNotOwnCallerStream()
    {
        using var root = new OwnedFileSystemRoot();
        var sut = new FileSystemBlobStoreProvider(root.Options);
        var key = new BlobKey("catalog", "source");
        (await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([1, 2]) })).IsSuccess.ShouldBeTrue();
        using var source = new FailingSource();
        (await sut.UploadAsync(new() { Key = key, Content = source })).HasError<FileSystemStorageUnavailableError>().ShouldBeTrue();
        source.CanRead.ShouldBeTrue();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.UploadAsync(new() { Key = key, Content = new MemoryStream([8]) }, canceled.Token));
        var download = await sut.DownloadAsync(key);
        await using var reader = download.Value.Content;
        reader.ReadByte().ShouldBe(1);
        reader.ReadByte().ShouldBe(2);
        Directory.EnumerateFiles(root.Path, "copy-*.tmp", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [LocalFileSystemFact]
    public async Task DocumentQueries_PruneTypeAndPartition_AndRejectUnfinishedCounts()
    {
        using var root = new OwnedFileSystemRoot();
        var options = root.Options;
        options.MaxQueryMetadataEntries = 1;
        var sut = new FileSystemDocumentStoreProvider(options);
        var type = DocumentTypeIdentity.For<Model>();

        for (var i = 0; i < 5; i++)
        {
            (await sut.UpsertAsync(type, new() { Key = new("partition-" + i, "row"), Content = [1] })).IsSuccess.ShouldBeTrue();
        }

        var count = await sut.CountAsync(type, new() { DocumentKey = new("partition-0", "r"), Filter = DocumentKeyFilter.RowKeyPrefixMatch }, DateTimeOffset.UtcNow);
        count.IsSuccess.ShouldBeTrue();
        count.Value.ShouldBe(1);
        var broad = await sut.CountAsync(type, new() { AllowFullScan = true }, DateTimeOffset.UtcNow);
        broad.HasError<DocumentStoreInvalidQueryError>().ShouldBeTrue();
    }

    [LocalFileSystemFact]
    public async Task DocumentPages_ConcurrentReplaceAndDelete_ReturnCompleteMatchingGenerations()
    {
        using var root = new OwnedFileSystemRoot();
        var writer = new FileSystemDocumentStoreProvider(root.Options);
        var reader = new FileSystemDocumentStoreProvider(root.Options);
        var type = DocumentTypeIdentity.For<Model>();
        var key = new DocumentKey("concurrent", "record");
        var write = Task.Run(async () =>
        {
            for (var i = 0; i < 40; i++)
            {
                var bytes = Enumerable.Repeat((byte)i, 32768).ToArray();
                var hash = "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
                (await writer.UpsertAsync(type, new() { Key = key, Content = bytes, StoredContentHash = hash })).IsSuccess.ShouldBeTrue();

                if (i % 5 == 0)
                {
                    (await writer.DeleteAsync(type, key)).IsSuccess.ShouldBeTrue();
                }
            }
        });

        for (var i = 0; i < 80; i++)
        {
            var page = await reader.FindPageAsync(type, new() { DocumentKey = key, Take = 1 }, DateTimeOffset.UtcNow);
            page.IsSuccess.ShouldBeTrue();

            foreach (var document in page.Value.Items)
            {
                ("sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(document.Content))).ShouldBe(document.StoredContentHash);
            }
        }

        await write;
        var invalid = await reader.ListPageAsync(type, new() { DocumentKey = key, ContinuationToken = "malformed" }, DateTimeOffset.UtcNow);
        invalid.HasError<DocumentStoreInvalidContinuationTokenError>().ShouldBeTrue();
    }

    [LocalFileSystemFact]
    public async Task Retention_OneStepBudget_ResumesToActualRecordsAndWraps()
    {
        using var root = new OwnedFileSystemRoot();
        var options = root.Options;
        options.MaxRetentionMetadataEntries = 1;
        var sut = new FileSystemBlobStoreProvider(options);

        for (var i = 0; i < 3; i++)
        {
            (await sut.UploadAsync(new() { Key = new("catalog", "due-" + i), Content = new MemoryStream([1]), ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) })).IsSuccess.ShouldBeTrue();
        }

        var deleted = new HashSet<BlobKey>();

        for (var i = 0; i < 256 && deleted.Count < 3; i++)
        {
            var restarted = new FileSystemBlobStoreProvider(options);
            var swept = await restarted.SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow });
            swept.IsSuccess.ShouldBeTrue();

            foreach (var key in swept.Value.DeletedKeys)
            {
                deleted.Add(key);
            }
        }

        deleted.Count.ShouldBe(3);
        Directory.EnumerateFiles(root.Path, "payload-*.bin", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [LocalFileSystemFact]
    public async Task CursorBudgetAndCorruption_BoundsWorkAndRejectsTraversal()
    {
        using var root = new OwnedFileSystemRoot();
        var options = root.Options;
        options.MaxRetentionMetadataEntries = 3;
        var sut = new FileSystemBlobStoreProvider(options);
        (await sut.UploadAsync(new() { Key = new("catalog", "one"), Content = new MemoryStream([1]) })).IsSuccess.ShouldBeTrue();

        for (var i = 0; i < 20; i++)
        {
            var page = await sut.Store.RetentionCandidatesAsync("blobs", null, default);
            page.Inspected.ShouldBeLessThanOrEqualTo(3);
            page.Paths.Count.ShouldBeLessThanOrEqualTo(3);
        }

        var cursor = Directory.EnumerateFiles(root.Path, "cursor.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(cursor, "[{\"Directory\":\"../outside\",\"Level\":0,\"Prefix\":\"\"}]");
        (await sut.SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow })).HasError<FileSystemStorageCorruptionError>().ShouldBeTrue();
    }

    public sealed class Model
    {
        public string Name { get; set; }
    }

    private sealed class FailingSource() : MemoryStream(new byte[65536])
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return this.Position == 0 ? base.ReadAsync(buffer, cancellationToken) : throw new IOException("Injected source failure.");
        }
    }
}