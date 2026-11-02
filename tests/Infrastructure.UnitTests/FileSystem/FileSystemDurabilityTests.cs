namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using Application.Storage;
using Infrastructure.FileSystem;

public sealed class FileSystemDurabilityTests
{
    [LocalFileSystemFact]
    public async Task BufferedReclamation_FailedCheckpoint_KeepsThePriorCompleteGenerationForNamespaceRollback()
    {
        using var root = new OwnedFileSystemRoot();
        var durable = new FileSystemBlobStoreProvider(root.Options);
        var key = new BlobKey("catalog", "buffered-checkpoint");
        (await durable.UploadAsync(new() { Key = key, Content = new MemoryStream([1]) })).IsSuccess.ShouldBeTrue();
        var path = durable.Store.Resolve(["blobs", "catalog", "buffered-checkpoint"], false);
        var oldManifest = File.ReadAllBytes(Path.Combine(path, "current.json"));
        var options = root.Options;
        options.Durability = FileSystemStorageDurability.Buffered;
        var buffered = new FileSystemBlobStoreProvider(options);
        var observed = new List<string>();
        buffered.Store.Fault = point =>
        {
            observed.Add(point);

            if (point == "before-reclamation-checkpoint")
            {
                throw new IOException("Checkpoint unavailable.");
            }
        };
        (await buffered.UploadAsync(new() { Key = key, Content = new MemoryStream([2]) })).IsSuccess.ShouldBeTrue();
        observed.IndexOf("manifest-flushed").ShouldBeLessThan(observed.IndexOf("published"));
        Directory.EnumerateFiles(path, "payload-*.bin").Count().ShouldBe(2);
        var unknownHead = new FileSystemBlobStoreProvider(root.Options);
        unknownHead.Store.Fault = point =>
        {
            if (point == "before-reclamation-checkpoint")
            {
                throw new IOException("A new provider cannot assume the old head was durably acknowledged.");
            }
        };
        using (var claim = await unknownHead.Store.AcquireAsync(path, false, default))
        {
            unknownHead.Store.TryCollect(path, claim);
        }

        Directory.EnumerateFiles(path, "payload-*.bin").Count().ShouldBe(2);
        File.WriteAllBytes(Path.Combine(path, "current.json"), oldManifest); // Model a permitted Buffered namespace rollback, not a power-loss experiment.
        var recovered = await new FileSystemBlobStoreProvider(root.Options).DownloadAsync(key);
        recovered.IsSuccess.ShouldBeTrue();
        await using var original = recovered.Value.Content;
        original.ReadByte().ShouldBe(1);
        buffered.Store.Fault = null;
        (await buffered.UploadAsync(new() { Key = key, Content = new MemoryStream([3]) })).IsSuccess.ShouldBeTrue();
        Directory.EnumerateFiles(path, "payload-*.bin").Count().ShouldBe(1);
    }

    [LocalFileSystemFact]
    public async Task BusyAuthority_ReportsConfiguredAcquisitionTimeout()
    {
        using var root = new OwnedFileSystemRoot();
        var options = root.Options;
        options.LockAcquireTimeout = TimeSpan.FromMilliseconds(50);
        var sut = new FileSystemBlobStoreProvider(options);
        var key = new BlobKey("catalog", "busy-budget");
        (await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([1]) })).IsSuccess.ShouldBeTrue();
        var path = sut.Store.Resolve(["blobs", "catalog", "busy-budget"], false);
        using var owner = await sut.Store.AcquireAsync(path, false, default);
        var result = await new FileSystemBlobStoreProvider(options).UpdatePropertiesAsync(new() { Key = key });
        result.Errors.OfType<BlobStoreTimeoutError>().Single().Timeout.ShouldBe(options.LockAcquireTimeout);
    }

    [HostFileSystemFact("Linux")]
    public void LinuxNoReplace_ExistingTarget_PreservesBothFilesWithoutFallback()
    {
        using var root = new OwnedFileSystemRoot();
        var source = Path.Combine(root.Path, "source");
        var target = Path.Combine(root.Path, "target");
        File.WriteAllBytes(source, [1]);
        File.WriteAllBytes(target, [2]);
        var error = Assert.Throws<FileSystemNativeIOException>(() => FileSystemDurability.Move(source, target, false, true));
        error.Code.ShouldBe(17);
        File.ReadAllBytes(source).ShouldBe(new byte[] { 1 });
        File.ReadAllBytes(target).ShouldBe(new byte[] { 2 });
    }

    [LocalFileSystemFact]
    public async Task SharedNamespaceOwners_ConcurrentAbsentMarkerAcquisition_NeverOverlap()
    {
        using var root = new OwnedFileSystemRoot();
        var options = root.Options;
        options.AccessKind = FileSystemStorageAccessKind.Smb;
        var store = new FileSystemStore(options);
        var path = store.Resolve(["blobs", "catalog", "contended"]);
        var active = 0;
        var overlaps = 0;
        var entries = 0;
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
        {
            for (var i = 0; i < 8; i++)
            {
                using var authority = await store.AcquireAsync(path, false, default);

                if (Interlocked.Increment(ref active) != 1)
                {
                    Interlocked.Increment(ref overlaps);
                }

                await Task.Delay(1);
                Interlocked.Increment(ref entries);
                Interlocked.Decrement(ref active);
            }
        })));
        overlaps.ShouldBe(0);
        entries.ShouldBe(128);
    }

    [LocalFileSystemFact]
    public async Task ConfirmedCommit_CleanupPrimitiveFailure_PreservesSuccessAndRetriesCleanupOnLaterVisit()
    {
        using var root = new OwnedFileSystemRoot();
        var sut = new FileSystemBlobStoreProvider(root.Options);
        var key = new BlobKey("catalog", "cleanup");
        (await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([1]) })).IsSuccess.ShouldBeTrue();
        sut.Store.Fault = point =>
        {
            if (point == "before-reclamation")
            {
                throw new FileSystemConfigurationException("Cleanup primitive unavailable.");
            }
        };
        (await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([2]) })).IsSuccess.ShouldBeTrue();
        var path = sut.Store.Resolve(["blobs", "catalog", "cleanup"], false);
        Directory.EnumerateFiles(path, "payload-*.bin").Count().ShouldBe(2);
        sut.Store.Fault = null;
        (await sut.UpdatePropertiesAsync(new() { Key = key })).IsSuccess.ShouldBeTrue();
        Directory.EnumerateFiles(path, "payload-*.bin").Count().ShouldBe(1);
    }

    [LocalFileSystemFact]
    public async Task SharedRetentionCursor_LostPublicationAcknowledgement_RetainsOwnerUntilQuiescentRecovery()
    {
        using var root = new OwnedFileSystemRoot();
        var options = root.Options;
        options.AccessKind = FileSystemStorageAccessKind.Smb;
        var sut = new FileSystemBlobStoreProvider(options);
        var key = new BlobKey("catalog", "due");
        (await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([1]), ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) })).IsSuccess.ShouldBeTrue();
        sut.Store.Fault = point =>
        {
            if (point == "cursor-published")
            {
                throw new IOException("Lost checkpoint acknowledgement.");
            }
        };
        (await sut.SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow })).HasError<FileSystemStorageUnavailableError>().ShouldBeTrue();
        Directory.EnumerateFiles(Path.Combine(root.Path, "retention"), "authority", SearchOption.AllDirectories).Count().ShouldBe(1);
        sut.Store.Fault = null;
        (await sut.SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow })).Value.DeletedCount.ShouldBe(0);
        await sut.Store.RecoverQuiescentAsync(true, default);
        (await sut.SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow })).Value.DeletedKeys.ShouldContain(key);
    }

    [LocalFileSystemFact]
    public async Task DurableRepeatedPublication_OpenMetadataAndPayloadHandles_KeepOldBytesAndReclaimReplacedGenerations()
    {
        using var root = new OwnedFileSystemRoot();
        var sut = new FileSystemBlobStoreProvider(root.Options);
        var key = new BlobKey("catalog", "pinned");
        (await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([1]) })).IsSuccess.ShouldBeTrue();
        var path = sut.Store.Resolve(["blobs", "catalog", "pinned"], false);
        await using var oldManifest = new FileStream(Path.Combine(path, "current.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var opened = await sut.DownloadAsync(key);
        await using var oldPayload = opened.Value.Content;

        for (var i = 0; i < 40; i++)
        {
            (await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([(byte)i]) })).IsSuccess.ShouldBeTrue();
            (await sut.UpdatePropertiesAsync(new() { Key = key, Properties = new() { ["iteration"] = i } })).IsSuccess.ShouldBeTrue();
        }

        oldPayload.ReadByte().ShouldBe(1);
        using var oldJson = System.Text.Json.JsonDocument.Parse(oldManifest);
        oldJson.RootElement.GetProperty("Blob").GetProperty("ETag").GetString().ShouldBe(opened.Value.Info.ETag);
        Directory.EnumerateFiles(path, "payload-*.bin").Count().ShouldBe(1);
        Directory.EnumerateFiles(path, "copy-*.tmp").ShouldBeEmpty();
        Directory.EnumerateFiles(path, "stage-*.owner").ShouldBeEmpty();
        (await sut.DeleteAsync(key)).IsSuccess.ShouldBeTrue();
        Directory.EnumerateFiles(path, "payload-*.bin").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("content-staged", false)]
    [InlineData("content-flushed", false)]
    [InlineData("manifest-staged", false)]
    [InlineData("manifest-flushed", false)]
    [InlineData("before-publication", false)]
    [InlineData("published", true)]
    [InlineData("commit-flushed", true)]
    [InlineData("acknowledged", true)]
    public async Task Upload_InjectedBoundaryFailure_LeavesCompleteOldOrNewGeneration(string boundary, bool newGeneration)
    {
        using var root = new OwnedFileSystemRoot();
        var sut = new FileSystemBlobStoreProvider(root.Options);
        var key = new BlobKey("catalog", "fault");
        (await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([1, 2]) })).IsSuccess.ShouldBeTrue();
        sut.Store.Fault = point =>
        {
            if (point == boundary)
            {
                throw new IOException("Injected boundary failure.");
            }
        };
        var failed = await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([8, 9, 10]) });
        failed.IsFailure.ShouldBeTrue();

        if (newGeneration)
        {
            failed.HasError<BlobStoreIndeterminateOperationError>().ShouldBeTrue();
        }

        var restarted = new FileSystemBlobStoreProvider(root.Options);
        var download = await restarted.DownloadAsync(key);
        download.IsSuccess.ShouldBeTrue();
        await using var reader = download.Value.Content;
        using var bytes = new MemoryStream();
        await reader.CopyToAsync(bytes);
        bytes.ToArray().ShouldBe(newGeneration ? new byte[] { 8, 9, 10 } : [1, 2]);
        download.Value.Info.Length.ShouldBe(bytes.Length);
    }

    [LocalFileSystemFact]
    public async Task BufferedToDurableMetadataUpdate_FlushesReferencedPayload_AndDeletePublishesTombstone()
    {
        using var root = new OwnedFileSystemRoot();
        var buffered = root.Options;
        buffered.Durability = FileSystemStorageDurability.Buffered;
        var writer = new FileSystemBlobStoreProvider(buffered);
        var key = new BlobKey("catalog", "promote");
        (await writer.UploadAsync(new() { Key = key, Content = new MemoryStream([4, 5]) })).IsSuccess.ShouldBeTrue();
        var sut = new FileSystemBlobStoreProvider(root.Options);
        var boundaries = new List<string>();
        sut.Store.Fault = boundaries.Add;
        (await sut.UpdatePropertiesAsync(new() { Key = key, Properties = new() { ["counter"] = 7m } })).IsSuccess.ShouldBeTrue();
        boundaries.IndexOf("referenced-payload-flushed").ShouldBeLessThan(boundaries.IndexOf("before-publication"));
        var path = sut.Store.Resolve(["blobs", "catalog", "promote"], false);
        (await sut.DeleteAsync(key)).IsSuccess.ShouldBeTrue();
        File.ReadAllText(Path.Combine(path, "current.json")).ShouldContain("\"Deleted\":true");
        Directory.EnumerateFiles(path, "payload-*.bin").ShouldBeEmpty();
        (await new FileSystemBlobStoreProvider(root.Options).ExistsAsync(key)).Value.ShouldBeFalse();
    }

    [LocalFileSystemFact]
    public async Task SharedOwner_IndeterminatePublication_RetainsAuthorityUntilExplicitQuiescentRecovery()
    {
        using var root = new OwnedFileSystemRoot();
        var options = root.Options;
        options.AccessKind = FileSystemStorageAccessKind.Smb; // Protocol implementation on local disk, not SMB qualification.
        options.LockAcquireTimeout = TimeSpan.Zero;
        var sut = new FileSystemBlobStoreProvider(options);
        var key = new BlobKey("catalog", "owner");
        sut.Store.Fault = point =>
        {
            if (point == "published")
            {
                throw new IOException("Lost acknowledgement.");
            }
        };
        var uncertain = await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([7]) });
        uncertain.HasError<BlobStoreIndeterminateOperationError>().ShouldBeTrue();
        var path = sut.Store.Resolve(["blobs", "catalog", "owner"], false);
        File.Exists(Path.Combine(path, "authority")).ShouldBeTrue();
        var second = new FileSystemBlobStoreProvider(options);
        var blocked = await second.UpdatePropertiesAsync(new() { Key = key });
        blocked.HasError<BlobStoreTimeoutError>().ShouldBeTrue();
        (await second.UploadAsync(new() { Key = new("catalog", "unrelated"), Content = new MemoryStream([1]) })).IsSuccess.ShouldBeTrue();
        await Assert.ThrowsAsync<FileSystemConfigurationException>(() => second.Store.RecoverQuiescentAsync(false, default));
        await second.Store.RecoverQuiescentAsync(true, default);
        (await second.UpdatePropertiesAsync(new() { Key = key })).IsSuccess.ShouldBeTrue();
        using var stale = await second.Store.AcquireAsync(path, false, default);
        stale.Dispose();
        Assert.Throws<InvalidOperationException>(() => second.Store.Publish(path, null, stale, default));
    }

    [LocalFileSystemFact]
    public async Task Cleanup_LiveStageAndOpenReader_ProtectsStageAndUnlinksSupersededGeneration()
    {
        using var root = new OwnedFileSystemRoot();
        var sut = new FileSystemBlobStoreProvider(root.Options);
        var key = new BlobKey("catalog", "stream");
        (await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([1, 2, 3]) })).IsSuccess.ShouldBeTrue();
        var original = await sut.DownloadAsync(key);
        await using var reader = original.Value.Content;
        var path = sut.Store.Resolve(["blobs", "catalog", "stream"], false);
        using (var staged = await sut.Store.StageAsync(path, new MemoryStream([5, 6]), null, default))
        {
            using var authority = await sut.Store.AcquireAsync(path, false, default);
            sut.Store.Collect(path, authority);
            File.Exists(Path.Combine(path, staged.File)).ShouldBeTrue();
        }

        (await sut.UploadAsync(new() { Key = key, Content = new MemoryStream([9]) })).IsSuccess.ShouldBeTrue();
        var bytes = new byte[3];
        await reader.ReadExactlyAsync(bytes);
        bytes.ShouldBe(new byte[] { 1, 2, 3 });
        await reader.DisposeAsync();
        Directory.EnumerateFiles(path, "payload-*.bin").Count().ShouldBe(1);
        var generation = Guid.NewGuid().ToString("N");
        File.WriteAllBytes(Path.Combine(path, $"copy-{generation}.tmp"), [1, 2]);
        using var claim = await sut.Store.AcquireAsync(path, false, default);
        sut.Store.Collect(path, claim);
        File.Exists(Path.Combine(path, $"copy-{generation}.tmp")).ShouldBeFalse();
    }

    [LocalFileSystemFact]
    public async Task CorruptionAndConfigurationFailures_AreDistinct_AndScansPruneOtherContainers()
    {
        using var root = new OwnedFileSystemRoot();
        var options = root.Options;
        options.MaxQueryMetadataEntries = 1;
        var sut = new FileSystemBlobStoreProvider(options, new() { AllowFullScans = true });

        for (var i = 0; i < 5; i++)
        {
            (await sut.UploadAsync(new() { Key = new("container-" + i, "one"), Content = new MemoryStream([1]) })).IsSuccess.ShouldBeTrue();
        }

        var page = await sut.ListPageAsync(new() { Container = "container-0", AllowFullScan = true });
        page.IsSuccess.ShouldBeTrue();
        page.Value.Items.Count.ShouldBe(1);
        var path = sut.Store.Resolve(["blobs", "container-0", "one"], false);
        File.WriteAllText(Path.Combine(path, "current.json"), "{}");
        (await sut.GetPropertiesAsync(new("container-0", "one"))).HasError<FileSystemStorageCorruptionError>().ShouldBeTrue();
        File.WriteAllText(Path.Combine(root.Path, "store.json"), "{\"version\":999,\"accessKind\":\"Local\",\"storeId\":\"" + Guid.NewGuid().ToString("N") + "\"}");
        (await new FileSystemBlobStoreProvider(root.Options).ExistsAsync(new("container-0", "one"))).HasError<FileSystemStorageConfigurationError>().ShouldBeTrue();
    }
}