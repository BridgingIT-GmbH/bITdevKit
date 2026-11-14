// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using System.Security.Cryptography;
using System.Buffers.Binary;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

public class FileSystemCacheStoreTests
{
    /// <summary>Starts the selected lifetime after payload finalization while preserving jitter, sliding and negative-marker durations.</summary>
    /// <param name="sliding">Whether the initial idle interval is shorter than the selected hard cap.</param>
    /// <param name="notFound">Whether the publication stores a metadata-only negative marker.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PublishAsync_DelayedFinalization_StartsSelectedLifetimeAtPublication(bool sliding, bool notFound)
    {
        // Arrange
        await using var fixture = await Fixture.ReadyAsync(negative: notFound,
            expiration: new() { SlidingEnabled = sliding, JitterRatio = 0.5 });
        var sut = fixture.Provider;
        var captured = await sut.CaptureFenceAsync(fixture.Identity);
        captured.IsSuccess.ShouldBeTrue();
        var started = await sut.BeginWriteAsync(new(fixture.Identity, captured.Value, default, 4096));
        started.IsSuccess.ShouldBeTrue();
        await using var session = started.Value;
        var bytes = notFound ? Array.Empty<byte>() : new byte[] { 4, 2 };
        await session.Stream.WriteAsync(bytes);
        var absolute = TimeSpan.FromMilliseconds(7500); // Preserve the earlier, once-only jitter selection.
        var initial = sliding ? TimeSpan.FromSeconds(2) : absolute;
        var metadata = fixture.Metadata(session, bytes, fixture.Identity, notFound) with
        {
            ExpiresAt = fixture.Clock.Now.Add(initial), AbsoluteExpiresAt = fixture.Clock.Now.Add(absolute),
            SlidingIdleTimeout = sliding ? initial : null
        };
        var delays = 0;
        sut.Fault = phase =>
        {
            if (phase != "payload-staged") { return; }

            delays++;
            fixture.Clock.Now = fixture.Clock.Now.AddSeconds(10);
        };

        // Act
        var published = await session.PublishAsync(metadata);
        sut.Fault = null;
        var acquired = await sut.LookupAsync(fixture.Identity, new(OpenPayload: !notFound, ObserveAccess: false));

        // Assert
        delays.ShouldBe(1);
        fixture.Clock.Now.ShouldBe(metadata.PublishedAt.AddSeconds(10));
        published.IsSuccess.ShouldBeTrue(string.Join(", ", published.Errors.Select(error => error.Message)));
        published.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        published.Value.Metadata.PublishedAt.ShouldBe(fixture.Clock.Now);
        published.Value.Metadata.ExpiresAt.ShouldBe(fixture.Clock.Now.Add(initial));
        published.Value.Metadata.AbsoluteExpiresAt.ShouldBe(fixture.Clock.Now.Add(absolute));
        acquired.IsSuccess.ShouldBeTrue();
        acquired.Value.State.ShouldBe(CacheReadValidationState.Valid);
        acquired.Value.Metadata.Version.ShouldBe(session.Version);
        acquired.Value.Metadata.PublishedAt.ShouldBe(fixture.Clock.Now);
        acquired.Value.Metadata.ExpiresAt.ShouldBe(fixture.Clock.Now.Add(initial));
        acquired.Value.Metadata.AbsoluteExpiresAt.ShouldBe(fixture.Clock.Now.Add(absolute));
        if (notFound) { acquired.Value.Handle.ShouldBeNull(); }
        else
        {
            await using var handle = acquired.Value.Handle;
            using var copy = new MemoryStream();
            await handle.Stream.CopyToAsync(copy);
            copy.ToArray().ShouldBe(bytes);
        }

        fixture.Clock.Now += initial;
        (await sut.LookupAsync(fixture.Identity, new(ObserveAccess: false))).Value.State.ShouldBe(CacheReadValidationState.Missing);
        (await sut.ReleaseFenceAsync(fixture.Identity, captured.Value)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>Reads a cold key while another mutation has exposed only its stage directory, before its first shard bootstrap.</summary>
    [Fact]
    public async Task Lookup_DuringFirstMutationBootstrap_IsMissingUntilItsPublicationFenceExists()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        fixture.Provider.Fault = phase =>
        {
            if (phase != "credit-reserved") { return; }

            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(15))) { throw new TimeoutException("The controlled first-shard bootstrap was not released."); }
        };
        var capture = Task.Run(async () => await fixture.Provider.CaptureFenceAsync(fixture.Identity));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            File.Exists(Path.Combine(fixture.Root.ShardDirectory(fixture.Identity), "header.json")).ShouldBeFalse();

            var result = await fixture.Provider.LookupAsync(fixture.Identity, new(ObserveAccess: false));

            result.IsSuccess.ShouldBeTrue();
            result.Value.State.ShouldBe(CacheReadValidationState.Missing);
        }
        finally
        {
            release.Set();
            fixture.Provider.Fault = null;
            (await capture).IsSuccess.ShouldBeTrue();
        }
    }

    /// <summary>A durable publication receipt prevents lost committed shard files from becoming a clean miss or a new bootstrap.</summary>
    /// <param name="loss">The committed directory or metadata file removed outside the provider.</param>
    [Theory]
    [InlineData("directory")]
    [InlineData("stages")]
    [InlineData("header")]
    [InlineData("manifest")]
    public async Task Lookup_LostCommittedShardMetadata_RejectsCorruption(string loss)
    {
        await using var fixture = await Fixture.ReadyAsync();
        (await fixture.WriteAsync([1])).IsSuccess.ShouldBeTrue();
        var directory = fixture.Root.ShardDirectory(fixture.Identity);
        if (loss is "directory" or "stages")
        {
            Directory.Delete(directory, true);
            if (loss == "stages") { Directory.CreateDirectory(Path.Combine(directory, "stages")); }
        }
        else { File.Delete(Path.Combine(directory, loss + ".json")); }

        var result = await fixture.Provider.LookupAsync(fixture.Identity, new(ObserveAccess: false));

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Corruption);
        if (loss is not "manifest") { File.Exists(Path.Combine(directory, "header.json")).ShouldBeFalse(); }
    }

    [Fact]
    public async Task NativeMaintenance_ReturnsAnEndedPreStagingCreditWithoutInventingACommittedShard()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var transient = new ResourceBudget("inspect", 32 * CacheDefaults.MiB);
        var resident = new ResourceBudget("inspect-resident", 64 * CacheDefaults.MiB);
        using var blocks = new CacheIndexBlockCache(4 * CacheDefaults.MiB);
        using var header = await fixture.Root.OpenAsync(default);
        var ledger = new CacheRootQuotaLedger(fixture.Root, header.Value, header.Bytes, transient, resident, blocks, fixture.Clock);
        var id = Guid.NewGuid().ToString("N");
        var credit = new CacheSpaceCredit { Id = id, OperationId = id, Identity = fixture.Identity, NodeId = "ended-before-staging",
            Owner = "stage-mutate-" + id, Kind = CacheSpaceCreditKind.Metadata };
        await ledger.ReserveAsync(credit, 128 * CacheDefaults.KiB, default);
        await ledger.MarkNativeEndedAsync(id, credit.NodeId, default);
        Directory.Exists(fixture.Root.ShardDirectory(fixture.Identity)).ShouldBeFalse();

        (await fixture.Provider.MaintainAsync(new(1000, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(5)))).IsSuccess.ShouldBeTrue();

        (await ledger.GetCreditAsync(id, default)).State.ShouldBe(CacheSpaceCreditState.Released);
        Directory.Exists(fixture.Root.ShardDirectory(fixture.Identity)).ShouldBeFalse();
    }

    [Fact]
    public async Task OfflineRecovery_ReportsCheckedButMalformedJournalContentAsCorruptionAndKeepsTheRootFenced()
    {
        await using var fixture = await Fixture.ReadyAsync();
        (await fixture.WriteAsync(new byte[] { 1 })).IsSuccess.ShouldBeTrue();
        var transient = new ResourceBudget("inspect", 16 * CacheDefaults.MiB);
        var resident = new ResourceBudget("inspect-resident", 16 * CacheDefaults.MiB);
        var directory = fixture.Root.ShardDirectory(fixture.Identity);
        using var manifest = await CacheCheckedMetadata.ReadAsync<CacheManifest>(Path.Combine(directory, "manifest.json"), transient, resident, default);
        var path = Path.Combine(directory, manifest.Value.Journal + ".journal");
        var bytes = await File.ReadAllBytesAsync(path);
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16));
        Array.Fill(bytes, (byte)0xff, CacheJournal.HeaderBytes, length);
        SHA256.HashData(bytes.AsSpan(0, CacheJournal.HeaderBytes + length), bytes.AsSpan(CacheJournal.HeaderBytes + length, 32));
        await File.WriteAllBytesAsync(path, bytes);
        var sut = new FileSystemCacheRecovery(fixture.Options);
        var inspected = (await sut.GetStateAsync()).Value;

        var recovered = await sut.RecoverQuiescentAsync(inspected.StoreId, true);

        recovered.IsSuccess.ShouldBeTrue();
        recovered.Value.RequiresReset.ShouldBeTrue();
        recovered.Value.Completed.ShouldBeFalse();
        (await sut.GetStateAsync()).Value.RecoveryInProgress.ShouldBeTrue();
        await using var blocked = fixture.OtherProvider();
        (await blocked.InitializeAsync([fixture.Definition])).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task NativeMaintenance_ReclaimsObsoletePositiveAndNegativeReceiptsAndTheirEndedQuotaCredits()
    {
        await using var fixture = await Fixture.ReadyAsync(negative: true);
        var operations = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid().ToString("N")).ToArray();
        (await fixture.WriteAsync(new byte[] { 1 }, operationId: operations[0])).IsSuccess.ShouldBeTrue();
        (await fixture.WriteAsync(new byte[] { 2 }, operationId: operations[1])).IsSuccess.ShouldBeTrue();
        (await fixture.WriteAsync([], notFound: true, operationId: operations[2])).IsSuccess.ShouldBeTrue();
        foreach (var id in operations) { (await fixture.Provider.ReconcileAsync(id)).IsSuccess.ShouldBeTrue(); }

        (await fixture.Provider.RemoveAsync(fixture.Identity)).IsSuccess.ShouldBeTrue();
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(6);
        for (var pass = 0; pass < 8; pass++)
        {
            (await fixture.Provider.MaintainAsync(new(1000, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(5)))).IsSuccess.ShouldBeTrue();
        }

        foreach (var id in operations)
        {
            var expired = await fixture.Provider.ReconcileAsync(id);
            expired.IsFailure.ShouldBeTrue();
            expired.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.NotFound);
        }

        (await fixture.Provider.GetQuotaAsync()).Value.RetiredBytes.ShouldBe(0);
        (await fixture.WriteAsync(new byte[] { 3 })).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task OfflineRecovery_VerifiesCurrentPayloadsFencesOldInstancesAndRevokesCapturedSources()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var written = (await fixture.WriteAsync(new byte[] { 4, 5, 6 })).Value;
        var captured = (await fixture.Provider.CaptureFenceAsync(fixture.Identity)).Value;
        var sut = new FileSystemCacheRecovery(fixture.Options);
        var before = (await sut.GetStateAsync()).Value;

        var recovered = await sut.RecoverQuiescentAsync(before.StoreId, true);

        recovered.IsSuccess.ShouldBeTrue();
        recovered.Value.Completed.ShouldBeTrue();
        recovered.Value.VerifiedShards.ShouldBe(1);
        recovered.Value.RecoveryGeneration.ShouldBe(before.RecoveryGeneration + 1);
        (await fixture.Provider.LookupAsync(fixture.Identity, new(ObserveAccess: false))).IsFailure.ShouldBeTrue();
        await using var reopened = fixture.OtherProvider();
        (await reopened.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        var lookup = await reopened.LookupAsync(fixture.Identity, new(OpenPayload: true, ObserveAccess: false));
        lookup.IsSuccess.ShouldBeTrue();
        lookup.Value.Metadata.Version.ShouldBe(written.Version.Value);
        await using (var handle = lookup.Value.Handle)
        {
            using var bytes = new MemoryStream();
            await handle.Stream.CopyToAsync(bytes);
            bytes.ToArray().ShouldBe(new byte[] { 4, 5, 6 });
        }

        (await reopened.BeginWriteAsync(new(fixture.Identity, captured, default, 1024))).IsFailure.ShouldBeTrue();
        var repeated = await sut.RecoverQuiescentAsync(before.StoreId, true);
        repeated.Value.Completed.ShouldBeTrue();
        repeated.Value.RecoveryGeneration.ShouldBe(before.RecoveryGeneration + 2);
    }

    [Fact]
    public async Task OfflineRecovery_RequiresExplicitGlobalProofAndTheInspectedStoreIdentityBeforeChangingFiles()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var sut = new FileSystemCacheRecovery(fixture.Options);
        var before = (await sut.GetStateAsync()).Value;
        var original = await File.ReadAllBytesAsync(Path.Combine(fixture.Path, "cache-root.json"));

        (await sut.ResetQuiescentAsync(before.StoreId, false)).IsFailure.ShouldBeTrue();
        var wrongIdentity = await sut.RecoverQuiescentAsync(Guid.NewGuid().ToString("N"), true);

        wrongIdentity.IsFailure.ShouldBeTrue();
        wrongIdentity.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        (await File.ReadAllBytesAsync(Path.Combine(fixture.Path, "cache-root.json"))).ShouldBe(original);
        (await sut.GetStateAsync()).Value.ShouldBe(before);
    }

    [Fact]
    public async Task MissingCommittedRootLedger_CannotReinitializeQuotaAndRequiresExplicitDisposableReset()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var sut = new FileSystemCacheRecovery(fixture.Options);
        var before = (await sut.GetStateAsync()).Value;
        var manifest = Path.Combine(fixture.Path, "ledger", "manifest.json");
        File.Delete(manifest);
        await using var failed = fixture.OtherProvider();

        var initialized = await failed.InitializeAsync([fixture.Definition]);

        initialized.IsFailure.ShouldBeTrue();
        initialized.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Corruption);
        File.Exists(manifest).ShouldBeFalse();
        var reset = await sut.ResetQuiescentAsync(before.StoreId, true);
        reset.IsSuccess.ShouldBeTrue();
        reset.Value.Completed.ShouldBeTrue();
        reset.Value.StoreId.ShouldNotBe(before.StoreId);
        await using var fresh = fixture.OtherProvider();
        (await fresh.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        (await fresh.LookupAsync(fixture.Identity, new(ObserveAccess: false))).Value.State.ShouldBe(CacheReadValidationState.Missing);
    }

    [Theory]
    [InlineData("Durable")]
    [InlineData("Buffered")]
    public async Task OfflineRecovery_RejectsAnOlderReadableManifestInsteadOfRestoringInvalidatedData(string durability)
    {
        await using var fixture = await Fixture.ReadyAsync(durability: durability);
        (await fixture.WriteAsync(new byte[] { 1 })).IsSuccess.ShouldBeTrue();
        var manifestPath = Path.Combine(fixture.Root.ShardDirectory(fixture.Identity), "manifest.json");
        var old = await File.ReadAllBytesAsync(manifestPath);
        (await fixture.Provider.RemoveAsync(fixture.Identity)).IsSuccess.ShouldBeTrue();
        await File.WriteAllBytesAsync(manifestPath, old); // Simulates an internally valid older storage generation.
        var sut = new FileSystemCacheRecovery(fixture.Options);
        var before = (await sut.GetStateAsync()).Value;

        var recovered = await sut.RecoverQuiescentAsync(before.StoreId, true);

        recovered.IsSuccess.ShouldBeTrue();
        recovered.Value.Completed.ShouldBeFalse();
        recovered.Value.RequiresReset.ShouldBeTrue();
        (await sut.GetStateAsync()).Value.RecoveryInProgress.ShouldBeTrue();
        await using var blocked = fixture.OtherProvider();
        (await blocked.InitializeAsync([fixture.Definition])).IsFailure.ShouldBeTrue();
        var reset = await sut.ResetQuiescentAsync(before.StoreId, true);
        reset.Value.DiscardedShards.ShouldBe(1);
        await using var empty = fixture.OtherProvider();
        (await empty.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        (await empty.LookupAsync(fixture.Identity, new(ObserveAccess: false))).Value.State.ShouldBe(CacheReadValidationState.Missing);
    }

    [Fact]
    public async Task OfflineRecovery_TruncatesOnlyTheUncommittedJournalSuffixAndPreservesTheCurrentValue()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var written = (await fixture.WriteAsync(new byte[] { 8, 9 })).Value;
        var transient = new ResourceBudget("inspect", 16 * CacheDefaults.MiB);
        var resident = new ResourceBudget("inspect-resident", 16 * CacheDefaults.MiB);
        var directory = fixture.Root.ShardDirectory(fixture.Identity);
        using var manifest = await CacheCheckedMetadata.ReadAsync<CacheManifest>(Path.Combine(directory, "manifest.json"), transient, resident, default);
        var journalPath = Path.Combine(directory, manifest.Value.Journal + ".journal");
        await using (var journal = new FileStream(journalPath, FileMode.Append, FileAccess.Write)) { await journal.WriteAsync(new byte[] { 0xff, 0, 1, 2, 3 }); }

        var sut = new FileSystemCacheRecovery(fixture.Options);
        var state = (await sut.GetStateAsync()).Value;
        var recovered = await sut.RecoverQuiescentAsync(state.StoreId, true);

        recovered.Value.Completed.ShouldBeTrue();
        new FileInfo(journalPath).Length.ShouldBe(manifest.Value.JournalBytes);
        await using var reopened = fixture.OtherProvider();
        (await reopened.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        (await reopened.LookupAsync(fixture.Identity, new(ObserveAccess: false))).Value.Metadata.Version.ShouldBe(written.Version.Value);
    }

    [Fact]
    public async Task NativeOperationHistory_PrioritizesActiveWorkReapsTerminalRecordsAndRejectsActiveOnlyCapacity()
    {
        await using var fixture = await Fixture.ReadyAsync();
        await using var limited = new FileSystemCacheStoreProvider("files", fixture.Options, fixture.Clock,
            diagnostics: new() { MaximumOperationRecords = 2, OperationRetention = TimeSpan.FromSeconds(30) });
        (await limited.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        CacheProviderOperation Pending()
        {
            var id = Guid.NewGuid().ToString("N");
            var request = new CacheProviderInvalidation("site", "items", CacheSelection.Pattern("*"), fixture.Clock.Now, OperationId: id);
            return new() { Id = id, Collection = "items", Request = request, Cutoff = request.Cutoff };
        }

        var old = (await limited.SaveOperationAsync(Pending())).Value;
        var terminal = await limited.SaveOperationAsync(old with { State = CacheOperationState.Failed }, old.Revision);
        terminal.IsSuccess.ShouldBeTrue();
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(1);
        var active = (await limited.SaveOperationAsync(Pending())).Value;
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(1);
        var newest = await limited.SaveOperationAsync(Pending());
        newest.IsSuccess.ShouldBeTrue();
        (await limited.GetOperationAsync(old.Id)).IsFailure.ShouldBeTrue();
        (await limited.GetOperationsAsync(1)).Value.Single().Id.ShouldBe(active.Id);
        var rejected = await limited.SaveOperationAsync(Pending());
        rejected.IsFailure.ShouldBeTrue();
        rejected.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Capacity);
        var failed = await limited.SaveOperationAsync(active with { State = CacheOperationState.Failed }, active.Revision);
        failed.IsSuccess.ShouldBeTrue();
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(31);
        (await limited.MaintainAsync(new(128, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(5)))).IsSuccess.ShouldBeTrue();
        (await limited.GetOperationAsync(active.Id)).IsFailure.ShouldBeTrue();
        (await limited.GetOperationsAsync()).Value.Single().Id.ShouldBe(newest.Value.Id);
    }

    [Fact]
    public async Task NativeMaintenance_ExpiresCapturedSourceMetadataWithoutRestoringItsPublicationRights()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var request = fixture.Ownership("node", "interest");
        var owner = (await fixture.Provider.AcquireOwnershipAsync(request)).Value;
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(6);
        for (var pass = 0; pass < 5; pass++)
        {
            (await fixture.Provider.MaintainAsync(new(1000, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(5)))).IsSuccess.ShouldBeTrue();
        }

        (await fixture.Provider.ObserveOwnershipAsync(fixture.Ownership("node", "interest"), owner.FlightId)).Value.State.ShouldBe(CacheOwnershipState.Lost);
        (await fixture.Provider.BeginWriteAsync(new(fixture.Identity, owner.Fence, default, 1024, owner.Owner))).IsFailure.ShouldBeTrue();
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, owner.Fence)).IsSuccess.ShouldBeTrue();
        var next = await fixture.Provider.AcquireOwnershipAsync(fixture.Ownership("next", "fresh-interest"));
        next.IsSuccess.ShouldBeTrue();
        next.Value.State.ShouldBe(CacheOwnershipState.Owned);
        next.Value.FlightId.ShouldNotBe(owner.FlightId);
    }

    [Fact]
    public async Task NativeTrackedCancellation_FromAnotherNodeStopsBeforeACollectionBoundaryAndPreservesTheClaim()
    {
        await using var fixture = await Fixture.ReadyAsync();
        await using var other = fixture.OtherProvider();
        (await other.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        (await fixture.WriteAsync(new byte[] { 1 })).IsSuccess.ShouldBeTrue();
        var captured = (await fixture.Provider.CaptureFenceAsync(fixture.Identity)).Value;
        var id = Guid.NewGuid().ToString("N");
        var request = new CacheProviderInvalidation("site", "items", CacheSelection.Pattern("*"), fixture.Clock.Now, OperationId: id);
        var created = (await fixture.Provider.SaveOperationAsync(new() { Id = id, Collection = "items", Cutoff = request.Cutoff, Request = request })).Value;
        var running = (await fixture.Provider.SaveOperationAsync(created with { State = CacheOperationState.Running }, created.Revision)).Value;
        var cancelled = await other.SaveOperationAsync(running with { CancellationRequested = true }, running.Revision);
        cancelled.IsSuccess.ShouldBeTrue();
        cancelled.Value.State.ShouldBe(CacheOperationState.Running);
        (await other.InvalidateAsync(request)).IsFailure.ShouldBeTrue(); // A remote cancellation request does not transfer execution authority.
        var outcome = await fixture.Provider.InvalidateAsync(request);
        outcome.IsSuccess.ShouldBeTrue();
        outcome.Value.AffectedCount.ShouldBe(0);
        (await fixture.Provider.GetOperationAsync(id)).Value.State.ShouldBe(CacheOperationState.Cancelled);
        var begun = await fixture.Provider.BeginWriteAsync(new(fixture.Identity, captured, default, 1024));
        begun.IsSuccess.ShouldBeTrue();
        await using var stage = begun.Value;
        var acquired = await fixture.Provider.LookupAsync(fixture.Identity, new(ObserveAccess: false));
        acquired.Value.State.ShouldBe(CacheReadValidationState.Valid);
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, captured)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task NativePressure_RejectsNewPayloadsBeforeCleanupHeadroomIsConsumedAndRecoversCapacity()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var bytes = new byte[512 * 1024];
        var stored = 0;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var result = await fixture.WriteAsync(bytes);
            if (result.IsFailure)
            {
                result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Capacity);
                break;
            }

            stored++;
        }

        stored.ShouldBeGreaterThan(1);
        stored.ShouldBeLessThan(40);
        var full = (await fixture.Provider.GetQuotaAsync()).Value;
        full.RetiredBytes.ShouldBeGreaterThan(0);
        for (var pass = 0; pass < 8; pass++)
        {
            (await fixture.Provider.MaintainAsync(new(1000, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(5)))).IsSuccess.ShouldBeTrue();
        }

        var cleaned = (await fixture.Provider.GetQuotaAsync()).Value;
        cleaned.RetiredBytes.ShouldBe(0);
        (cleaned.CurrentBytes + cleaned.RetiredBytes + cleaned.MetadataBytes + cleaned.StagingBytes).ShouldBeLessThanOrEqualTo(cleaned.MaximumBytes);
        (await fixture.WriteAsync(bytes)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task NativeMaintenance_CompactsCheckedIndexesAndReturnsTheirOldMetadataCharges()
    {
        await using var fixture = await Fixture.ReadyAsync();
        CacheEntryVersion? latest = null;
        for (var index = 0; index < 11; index++)
        {
            var written = await fixture.WriteAsync(new byte[] { (byte)index });
            written.IsSuccess.ShouldBeTrue();
            latest = written.Value.Version;
        }

        var directory = fixture.Root.ShardDirectory(fixture.Identity);
        Directory.EnumerateFiles(directory, "*.idx").Count().ShouldBeGreaterThanOrEqualTo(32);
        for (var pass = 0; pass < 5; pass++)
        {
            (await fixture.Provider.MaintainAsync(new(1000, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(5)))).IsSuccess.ShouldBeTrue();
        }

        Directory.EnumerateFiles(directory, "*.idx").Count().ShouldBeLessThan(8);
        var read = await fixture.Provider.LookupAsync(fixture.Identity, new(OpenPayload: true, ObserveAccess: false));
        read.IsSuccess.ShouldBeTrue();
        read.Value.Metadata.Version.ShouldBe(latest.Value);
        await using var handle = read.Value.Handle;
        var bytes = new byte[1];
        await handle.Stream.ReadExactlyAsync(bytes);
        bytes[0].ShouldBe((byte)10);
        (await fixture.Provider.GetQuotaAsync()).Value.CurrentBytes.ShouldBe(1);
        var transient = new ResourceBudget("index-inspect", 16 * CacheDefaults.MiB);
        var resident = new ResourceBudget("index-inspect-resident", 16 * CacheDefaults.MiB);
        using var blocks = new CacheIndexBlockCache(4 * CacheDefaults.MiB);
        using var root = await fixture.Root.OpenAsync(default);
        var shard = new CacheShardStore(fixture.Root, directory, root.Value.StoreId, fixture.Identity.CollectionIdentity,
            CacheStoreRoot.Shard(fixture.Identity), root.Value.RecoveryGeneration, transient, resident, blocks);
        await using var transaction = await shard.AcquireAsync(false, null, null, default);
        var range = CacheMetadataIndex.PrefixRange(CacheMetadataIndex.LogicalPrefix(fixture.Identity));
        var candidates = new List<string>();
        await foreach (var row in transaction.ScanAsync(range.Lower, 100, default, range.Upper))
        {
            if (!row.Deleted) { candidates.Add(new Guid(row.Bytes.Span[32..], bigEndian: true).ToString("N")); }
        }

        candidates.ShouldBe(new[] { latest.Value.Value });
    }

    [Fact]
    public async Task NativeMaintenance_KeepsRetiredReaderBytesUntilTheActualHandleCloses()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var bytes = RandomNumberGenerator.GetBytes(140000);
        (await fixture.WriteAsync(bytes)).IsSuccess.ShouldBeTrue();
        var acquired = await fixture.Provider.LookupAsync(fixture.Identity, new(OpenPayload: true, ObserveAccess: false));
        acquired.IsSuccess.ShouldBeTrue();
        var handle = acquired.Value.Handle;
        (await fixture.Provider.RemoveAsync(fixture.Identity)).IsSuccess.ShouldBeTrue();
        for (var pass = 0; pass < 3; pass++)
        {
            (await fixture.Provider.MaintainAsync(new(1000, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(5)))).IsSuccess.ShouldBeTrue();
        }

        (await fixture.Provider.GetQuotaAsync()).Value.RetiredBytes.ShouldBe(bytes.Length);
        var actual = new byte[bytes.Length];
        await handle.Stream.ReadExactlyAsync(actual);
        actual.ShouldBe(bytes);
        await handle.DisposeAsync();
        for (var pass = 0; pass < 4; pass++)
        {
            (await fixture.Provider.MaintainAsync(new(1000, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(5)))).IsSuccess.ShouldBeTrue();
        }

        (await fixture.Provider.GetQuotaAsync()).Value.RetiredBytes.ShouldBe(0);
        Directory.EnumerateFiles(fixture.Root.ShardDirectory(fixture.Identity), "*.payload", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task NativeMaintenance_ReclaimsEndedUnpublishedStagingWithoutChangingTheCurrentVersion()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var previous = (await fixture.WriteAsync(new byte[] { 1 })).Value.Version;
        var fence = (await fixture.Provider.CaptureFenceAsync(fixture.Identity)).Value;
        var begun = await fixture.Provider.BeginWriteAsync(new(fixture.Identity, fence, default, 1024 * 1024));
        begun.IsSuccess.ShouldBeTrue();
        await begun.Value.Stream.WriteAsync(new byte[200000]);
        var version = begun.Value.Version;
        await begun.Value.DisposeAsync();
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, fence)).IsSuccess.ShouldBeTrue();
        var reclaimed = 0L;
        for (var pass = 0; pass < 5; pass++)
        {
            var result = await fixture.Provider.MaintainAsync(new(1000, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(5)));
            result.IsSuccess.ShouldBeTrue();
            reclaimed += result.Value.ReclaimedEntries;
        }

        reclaimed.ShouldBeGreaterThan(0);
        File.Exists(CachePayloadReference.PathFor(Path.Combine(fixture.Root.ShardDirectory(fixture.Identity), "values"), version.Value, true)).ShouldBeFalse();
        (await fixture.Provider.LookupAsync(fixture.Identity, new(ObserveAccess: false))).Value.Metadata.Version.ShouldBe(previous.Value);
    }

    [Fact]
    public async Task NativeCreateOnly_ReplacesConfirmedNegativeButRejectsAValidPositiveEmptyValue()
    {
        await using var fixture = await Fixture.ReadyAsync(negative: true);
        (await fixture.WriteAsync([], notFound: true)).IsSuccess.ShouldBeTrue();
        (await fixture.WriteAsync([], new(CreateOnly: true))).IsSuccess.ShouldBeTrue();
        var rejected = await fixture.WriteAsync(new byte[] { 1 }, new(CreateOnly: true));
        rejected.IsFailure.ShouldBeTrue();
        rejected.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        (await fixture.Provider.LookupAsync(fixture.Identity, new(ObserveAccess: false))).Value.Metadata.IsNotFound.ShouldBeFalse();
    }

    [Fact]
    public async Task FilterInvalidation_FencesAbsentWorkAndExcludesBackdatedPublicationsAfterTheBoundary()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var old = new CacheEntryIdentity("site", "items", "1", "old");
        var later = new CacheEntryIdentity("site", "items", "1", "later");
        (await fixture.WriteAsync(new byte[] { 1 }, identity: old)).IsSuccess.ShouldBeTrue();
        var captured = (await fixture.Provider.CaptureFenceAsync(fixture.Identity)).Value;
        var request = new CacheProviderInvalidation("site", "items", CacheSelection.Pattern("*"), fixture.Clock.Now);
        var first = await fixture.Provider.InvalidateAsync(request);
        first.IsSuccess.ShouldBeTrue();
        (await fixture.Provider.BeginWriteAsync(new(fixture.Identity, captured, default, 1024))).IsFailure.ShouldBeTrue();
        (await fixture.WriteAsync(new byte[] { 2 }, identity: later)).IsSuccess.ShouldBeTrue();
        var outcome = first.Value;
        for (var pass = 0; !outcome.Completed && pass < 300; pass++)
        {
            var next = await fixture.Provider.InvalidateAsync(request with { Continuation = outcome.Continuation });
            next.IsSuccess.ShouldBeTrue();
            outcome = next.Value;
        }

        outcome.Completed.ShouldBeTrue();
        outcome.AffectedCount.ShouldBe(1);
        (await fixture.Provider.LookupAsync(old, new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
        var remaining = await fixture.Provider.LookupAsync(later, new());
        remaining.IsSuccess.ShouldBeTrue();
        remaining.Value.State.ShouldBe(CacheReadValidationState.Valid);
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, captured)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task TrackedInvalidation_ClaimsOneExecutorAndCommitsEachNativeBatchWithItsCheckpoint()
    {
        await using var fixture = await Fixture.ReadyAsync();
        await using var other = fixture.OtherProvider();
        (await other.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        for (var index = 0; index < 3; index++)
        {
            (await fixture.WriteAsync(new byte[] { 1 }, identity: new("site", "items", "1", "key:" + index))).IsSuccess.ShouldBeTrue();
        }

        var id = Guid.NewGuid().ToString("N");
        var request = new CacheProviderInvalidation("site", "items", CacheSelection.Pattern("*"), fixture.Clock.Now, OperationId: id);
        var created = await fixture.Provider.SaveOperationAsync(new() { Id = id, Collection = "items", Cutoff = request.Cutoff, Request = request,
            State = CacheOperationState.Queued });
        created.IsSuccess.ShouldBeTrue();
        var claimed = await fixture.Provider.SaveOperationAsync(created.Value with { State = CacheOperationState.Running }, created.Value.Revision);
        claimed.IsSuccess.ShouldBeTrue();
        (await other.SaveOperationAsync(claimed.Value, claimed.Value.Revision)).IsFailure.ShouldBeTrue();
        var boundary = await fixture.Provider.InvalidateAsync(request);
        boundary.IsSuccess.ShouldBeTrue();
        boundary.Value.Completed.ShouldBeFalse();
        boundary.Value.AffectedCount.ShouldBe(0);
        var status = (await fixture.Provider.GetOperationAsync(id)).Value;
        status.Outcome.Continuation.ShouldBe(boundary.Value.Continuation);
        status.Revision.ShouldNotBe(claimed.Value.Revision);
        var before = status.Outcome.Continuation;
        var batch = await fixture.Provider.InvalidateAsync(request with { Continuation = before });
        batch.IsSuccess.ShouldBeTrue();
        status = (await fixture.Provider.GetOperationAsync(id)).Value;
        status.Outcome.Continuation.ShouldBe(batch.Value.Continuation);
        status.Outcome.AffectedCount.ShouldBe(batch.Value.AffectedCount);
        (await fixture.Provider.InvalidateAsync(request with { Continuation = before })).IsFailure.ShouldBeTrue();
        for (var pass = 0; !status.Outcome.Completed && pass < 300; pass++)
        {
            var next = await fixture.Provider.InvalidateAsync(request with { Continuation = status.Outcome.Continuation });
            next.IsSuccess.ShouldBeTrue();
            status = (await fixture.Provider.GetOperationAsync(id)).Value;
            status.Outcome.Continuation.ShouldBe(next.Value.Continuation);
            status.Outcome.AffectedCount.ShouldBe(next.Value.AffectedCount);
            status.Outcome.Completed.ShouldBe(next.Value.Completed);
        }

        status.State.ShouldBe(CacheOperationState.Completed);
        status.Outcome.AffectedCount.ShouldBe(3);
        var listed = await fixture.Provider.GetOperationsAsync();
        listed.IsSuccess.ShouldBeTrue();
        listed.Value.Single().Id.ShouldBe(id);
        (await fixture.Provider.QueryAsync(new("site", "items", CacheSelection.Pattern("*"), fixture.Clock.Now))).Value.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task IndexedMetadataPages_UseSignedContinuationsAndNeverOpenPayloads()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var identities = new[] { "group:a", "group:b", "other", "group:😀", "group:?literal" }
            .Select(k => new CacheEntryIdentity("site", "items", "1", k)).ToArray();
        var locks = new List<FileStream>();
        try
        {
            foreach (var identity in identities)
            {
                var stored = await fixture.WriteAsync(new byte[] { 1 }, identity: identity);
                stored.IsSuccess.ShouldBeTrue();
                locks.Add(new(CachePayloadReference.PathFor(Path.Combine(fixture.Root.ShardDirectory(identity), "values"), stored.Value.Version.Value.Value),
                    FileMode.Open, FileAccess.Read, FileShare.None));
            }

            var query = new CacheMetadataQuery("site", "items", CacheSelection.Prefix("GROUP:"), fixture.Clock.Now, PageSize: 2);
            var found = new List<CacheEntryMetadata>();
            string continuation = null;
            CacheMetadataPage last;
            var pages = 0;
            do
            {
                var result = await fixture.Provider.QueryAsync(query with { Continuation = continuation });
                result.IsSuccess.ShouldBeTrue();
                last = result.Value;
                found.AddRange(last.Entries);
                continuation = last.Continuation;
                (++pages).ShouldBeLessThan(10);
            }
            while (continuation is not null);

            found.Select(m => m.Identity.Key).Order().ShouldBe(identities.Where(i => i.Key.StartsWith("GROUP:", StringComparison.Ordinal)).Select(i => i.Key).Order());
            found.Select(m => m.Version).Distinct().Count().ShouldBe(4);
            last.Completed.ShouldBeTrue();
            last.CountIsExact.ShouldBeTrue();
            last.Count.ShouldBe(4);
            var first = (await fixture.Provider.QueryAsync(query)).Value;
            var tampered = first.Continuation[..^2] + "AA";
            (await fixture.Provider.QueryAsync(query with { Continuation = tampered })).IsFailure.ShouldBeTrue();
            (await fixture.Provider.QueryAsync(query with { Selection = CacheSelection.Key("other"), Continuation = first.Continuation })).IsFailure.ShouldBeTrue();
        }
        finally { foreach (var held in locks) { await held.DisposeAsync(); } }
    }

    [Fact]
    public async Task QueryContinuation_ExcludesEvenBackdatedPublicationsAfterItsCapturedNativeBoundary()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var a = new CacheEntryIdentity("site", "items", "1", "a");
        var b = new CacheEntryIdentity("site", "items", "1", "b");
        var later = new CacheEntryIdentity("site", "items", "1", "later");
        (await fixture.WriteAsync(new byte[] { 1 }, identity: a)).IsSuccess.ShouldBeTrue();
        (await fixture.WriteAsync(new byte[] { 2 }, identity: b)).IsSuccess.ShouldBeTrue();
        var query = new CacheMetadataQuery("site", "items", CacheSelection.Pattern("*"), fixture.Clock.Now, PageSize: 1);
        var first = await fixture.Provider.QueryAsync(query);
        first.IsSuccess.ShouldBeTrue();
        first.Value.Continuation.ShouldNotBeNull();
        (await fixture.WriteAsync(new byte[] { 3 }, identity: later)).IsSuccess.ShouldBeTrue(); // Same publication timestamp as the earlier versions.
        var keys = first.Value.Entries.Select(m => m.Identity.Key).ToList();
        var next = first.Value.Continuation;
        for (var pages = 0; next is not null && pages < 10; pages++)
        {
            var result = await fixture.Provider.QueryAsync(query with { Continuation = next });
            result.IsSuccess.ShouldBeTrue();
            keys.AddRange(result.Value.Entries.Select(m => m.Identity.Key));
            next = result.Value.Continuation;
        }

        next.ShouldBeNull();
        keys.Order().ShouldBe(new[] { a.Key, b.Key }.Order());
    }

    [Fact]
    public async Task TypedSelections_PreserveNullEmptySetsAnyMembershipAndHalfOpenIntervalsWithoutDuplicates()
    {
        CacheDimensionSchema[] schemas = [new("tags", CacheDimensionKind.String, CacheDimensionForm.Set, AllowNull: true), new("range", CacheDimensionKind.Integer, CacheDimensionForm.Interval)];
        await using var fixture = await Fixture.ReadyAsync(dimensions: schemas);
        var a = new CacheEntryIdentity("site", "items", "1", "a");
        var b = new CacheEntryIdentity("site", "items", "1", "b");
        var c = new CacheEntryIdentity("site", "items", "1", "c");
        (await fixture.WriteAsync(new byte[] { 1 }, identity: a, dimensions:
            [CacheDimension.Set("tags", CacheDimensionKind.String, "west", "east", null), CacheDimension.Interval("range", -10L, 0L)])).IsSuccess.ShouldBeTrue();
        (await fixture.WriteAsync(new byte[] { 2 }, identity: b, dimensions:
            [CacheDimension.Set("tags", CacheDimensionKind.String), CacheDimension.Interval("range", 0L, 10L)])).IsSuccess.ShouldBeTrue();
        (await fixture.WriteAsync(new byte[] { 3 }, identity: c, dimensions: [CacheDimension.Interval("range", 10L, 20L)])).IsSuccess.ShouldBeTrue();
        var any = await fixture.Provider.QueryAsync(new("site", "items", CacheSelection.Dimension("tags").ContainsAny(CacheDimensionKind.String, "WEST", "EAST"), fixture.Clock.Now));
        any.IsSuccess.ShouldBeTrue();
        any.Value.Entries.Single().Identity.ShouldBe(a);
        (await fixture.Provider.QueryAsync(new("site", "items", CacheSelection.Dimension("tags").Contains(null, CacheDimensionKind.String), fixture.Clock.Now))).Value.Entries.Single().Identity.ShouldBe(a);
        (await fixture.Provider.QueryAsync(new("site", "items", CacheSelection.Dimension("tags").IsPresent(), fixture.Clock.Now))).Value.Entries.Count.ShouldBe(2);
        (await fixture.Provider.QueryAsync(new("site", "items", CacheSelection.Dimension("tags").IsAbsent(), fixture.Clock.Now))).Value.Entries.Single().Identity.ShouldBe(c);
        (await fixture.Provider.QueryAsync(new("site", "items", CacheSelection.Dimension("range").Overlaps(10L, 11L), fixture.Clock.Now))).Value.Entries.Single().Identity.ShouldBe(c);
    }

    [Fact]
    public async Task IndependentProviderInstances_ShareOneOwnerAndObserveItsConfirmedPublication()
    {
        await using var fixture = await Fixture.ReadyAsync();
        await using var second = fixture.OtherProvider();
        (await second.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        var firstRequest = fixture.Ownership("one", "one-interest");
        var secondRequest = fixture.Ownership("two", "two-interest");
        var owner = await fixture.Provider.AcquireOwnershipAsync(firstRequest);
        var follower = await second.AcquireOwnershipAsync(secondRequest);

        owner.IsSuccess.ShouldBeTrue();
        follower.IsSuccess.ShouldBeTrue();
        owner.Value.State.ShouldBe(CacheOwnershipState.Owned);
        follower.Value.State.ShouldBe(CacheOwnershipState.Pending);
        follower.Value.Owner.ShouldBeNull();
        follower.Value.FlightId.ShouldBe(owner.Value.FlightId);
        var published = await fixture.WriteOwnedAsync(new byte[] { 1, 2, 3 }, owner.Value);
        published.IsSuccess.ShouldBeTrue();
        (await fixture.Provider.CompleteOwnershipAsync(fixture.Identity, owner.Value.Owner.Value,
            new(CacheOwnershipState.Published, published.Value.Version))).IsSuccess.ShouldBeTrue();
        var observed = await second.ObserveOwnershipAsync(secondRequest, follower.Value.FlightId);
        observed.IsSuccess.ShouldBeTrue();
        observed.Value.State.ShouldBe(CacheOwnershipState.Published);
        observed.Value.Version.ShouldBe(published.Value.Version);
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, owner.Value.Fence)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SharedInterest_ProtectsTheOwnerAfterItsLocalGroupWithdrawsAndEndsWhenTheLastGroupLeaves()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var ownRequest = fixture.Ownership("one", "one-interest");
        var foreign = fixture.Ownership("two", "two-interest");
        var owner = (await fixture.Provider.AcquireOwnershipAsync(ownRequest)).Value;
        (await fixture.Provider.AcquireOwnershipAsync(foreign)).Value.State.ShouldBe(CacheOwnershipState.Pending);
        (await fixture.Provider.ReleaseInterestAsync(fixture.Identity, owner.FlightId, ownRequest.InterestId)).IsSuccess.ShouldBeTrue();

        (await fixture.Provider.RenewOwnershipAsync(fixture.Identity, owner.Owner.Value)).Value.State.ShouldBe(CacheOwnershipState.Owned);
        (await fixture.Provider.ReleaseInterestAsync(fixture.Identity, owner.FlightId, foreign.InterestId)).IsSuccess.ShouldBeTrue();
        var ended = await fixture.Provider.RenewOwnershipAsync(fixture.Identity, owner.Owner.Value);

        ended.IsSuccess.ShouldBeTrue();
        ended.Value.State.ShouldBe(CacheOwnershipState.Lost);
        ended.Value.Owner.ShouldBeNull();
        (await fixture.Provider.BeginWriteAsync(new(fixture.Identity, owner.Fence, default, 1024, owner.Owner))).IsFailure.ShouldBeTrue();
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, owner.Fence)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task KeyInvalidation_RevokesOldOwnershipAndANewFlightGetsDistinctConfirmedAuthority()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var request = fixture.Ownership("one", "interest");
        var old = (await fixture.Provider.AcquireOwnershipAsync(request)).Value;
        (await fixture.Provider.RemoveAsync(fixture.Identity)).IsSuccess.ShouldBeTrue();

        (await fixture.Provider.ObserveOwnershipAsync(request, old.FlightId)).Value.State.ShouldBe(CacheOwnershipState.Lost);
        var next = await fixture.Provider.AcquireOwnershipAsync(fixture.Ownership("two", "new-interest"));

        next.IsSuccess.ShouldBeTrue();
        next.Value.State.ShouldBe(CacheOwnershipState.Owned);
        next.Value.FlightId.ShouldNotBe(old.FlightId);
        next.Value.Owner.ShouldNotBe(old.Owner);
        (await fixture.Provider.BeginWriteAsync(new(fixture.Identity, old.Fence, default, 1024, old.Owner))).IsFailure.ShouldBeTrue();
        (await fixture.Provider.CompleteOwnershipAsync(fixture.Identity, next.Value.Owner.Value, new(CacheOwnershipState.Lost))).IsSuccess.ShouldBeTrue();
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, next.Value.Fence)).IsSuccess.ShouldBeTrue();
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, old.Fence)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RepeatedLeaseRenewal_NeverExtendsTheOriginalTwoMinuteSourceDeadline()
    {
        await using var fixture = await Fixture.ReadyAsync(sourceTimeout: TimeSpan.FromMinutes(2));
        var request = fixture.Ownership("one", "interest");
        var owner = (await fixture.Provider.AcquireOwnershipAsync(request)).Value;
        var previous = owner.LeaseExpiresAt;
        for (var index = 0; index < 5; index++)
        {
            fixture.Clock.Now = fixture.Clock.Now.AddSeconds(20);
            var renewal = await fixture.Provider.RenewOwnershipAsync(fixture.Identity, owner.Owner.Value);
            renewal.IsSuccess.ShouldBeTrue();
            renewal.Value.State.ShouldBe(CacheOwnershipState.Owned);
            renewal.Value.LeaseExpiresAt.ShouldBeGreaterThanOrEqualTo(previous);
            renewal.Value.LeaseExpiresAt.ShouldBeLessThanOrEqualTo(request.Deadline);
            previous = renewal.Value.LeaseExpiresAt;
        }

        fixture.Clock.Now = request.Deadline;
        (await fixture.Provider.RenewOwnershipAsync(fixture.Identity, owner.Owner.Value)).Value.State.ShouldBe(CacheOwnershipState.Lost);
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, owner.Fence)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ExactRemoval_FencesAllRetainedContractsAndKeepsAnAlreadyAcquiredReaderAlive()
    {
        await using var fixture = new Fixture();
        (await fixture.Provider.InitializeAsync([fixture.Definition, fixture.Definition with { ContractVersion = "2" }])).IsSuccess.ShouldBeTrue();
        var secondIdentity = new CacheEntryIdentity(fixture.Identity.Namespace, fixture.Identity.Collection, "2", fixture.Identity.Key);
        var a = await fixture.WriteAsync(new byte[] { 1 });
        var b = await fixture.WriteAsync(new byte[] { 2, 3 }, identity: secondIdentity);
        a.IsSuccess.ShouldBeTrue();
        b.IsSuccess.ShouldBeTrue();
        var acquired = await fixture.Provider.LookupAsync(fixture.Identity, new(OpenPayload: true));
        await using var pinned = acquired.Value.Handle;

        var removed = await fixture.Provider.RemoveAsync(fixture.Identity, new(a.Value.Version));

        removed.IsSuccess.ShouldBeTrue();
        removed.Value.Completed.ShouldBeTrue();
        removed.Value.AffectedCount.ShouldBe(2);
        (await fixture.Provider.LookupAsync(fixture.Identity, new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
        (await fixture.Provider.LookupAsync(secondIdentity, new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
        pinned.Stream.ReadByte().ShouldBe(1);
        (await fixture.Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(0);
    }

    [Fact]
    public async Task StaleExpectedRemoval_HasNoKeyFenceOrEntryEffect()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var stored = await fixture.WriteAsync(new byte[] { 1 });
        var fence = (await fixture.Provider.CaptureFenceAsync(fixture.Identity)).Value;
        var rejected = await fixture.Provider.RemoveAsync(fixture.Identity, new(CacheEntryVersion.New()));

        rejected.IsFailure.ShouldBeTrue();
        rejected.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        (await fixture.Provider.LookupAsync(fixture.Identity, new())).Value.Metadata.Version.ShouldBe(stored.Value.Version.Value);
        var stillAuthorized = await fixture.Provider.BeginWriteAsync(new(fixture.Identity, fence, default, 1024));
        stillAuthorized.IsSuccess.ShouldBeTrue();
        await stillAuthorized.Value.DisposeAsync();
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, fence)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RemovingAnAbsentKey_RevokesAnAlreadyStagedWriterWithoutPublishingItsBytes()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var fence = (await fixture.Provider.CaptureFenceAsync(fixture.Identity)).Value;
        var begun = await fixture.Provider.BeginWriteAsync(new(fixture.Identity, fence, default, 1024));
        begun.IsSuccess.ShouldBeTrue();
        await using var session = begun.Value;
        var bytes = new byte[] { 1, 2, 3 };
        await session.Stream.WriteAsync(bytes);
        var removed = await fixture.Provider.RemoveAsync(fixture.Identity);
        removed.IsSuccess.ShouldBeTrue();
        removed.Value.AffectedCount.ShouldBe(0);

        var late = await session.PublishAsync(fixture.Metadata(session, bytes, fixture.Identity));

        late.IsFailure.ShouldBeTrue();
        late.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        (await fixture.Provider.LookupAsync(fixture.Identity, new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, fence)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task NativeWriteSession_PublishesItsExactVersionAndReconcilesWithoutRepeatingBytes()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var bytes = Enumerable.Range(0, 131123).Select(i => (byte)(i % 251)).ToArray();
        var operationId = Guid.NewGuid().ToString("N");
        var stored = await fixture.WriteAsync(bytes, operationId: operationId);

        stored.IsSuccess.ShouldBeTrue();
        stored.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        var acquired = await fixture.Provider.LookupAsync(fixture.Identity, new(OpenPayload: true));
        acquired.IsSuccess.ShouldBeTrue();
        await using var handle = acquired.Value.Handle;
        handle.Metadata.Version.ShouldBe(stored.Value.Version.Value);
        var actual = new MemoryStream();
        await handle.Stream.CopyToAsync(actual);
        actual.ToArray().ShouldBe(bytes);
        (await fixture.Provider.GetQuotaAsync()).Value.CurrentBytes.ShouldBe(bytes.Length);
        var reconciled = await fixture.Provider.ReconcileAsync(operationId);
        reconciled.IsSuccess.ShouldBeTrue();
        reconciled.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        reconciled.Value.Version.ShouldBe(stored.Value.Version);
    }

    [Fact]
    public async Task ConditionalReplacement_PreservesAPinnedPreviousVersionAndRejectsItsStaleToken()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var first = await fixture.WriteAsync(new byte[] { 1, 2, 3 });
        first.IsSuccess.ShouldBeTrue();
        var acquired = await fixture.Provider.LookupAsync(fixture.Identity, new(OpenPayload: true));
        await using var pinned = acquired.Value.Handle;
        var next = await fixture.WriteAsync(new byte[] { 4, 5 }, new(first.Value.Version));

        next.IsSuccess.ShouldBeTrue();
        next.Value.Version.ShouldNotBe(first.Value.Version);
        (await fixture.WriteAsync(new byte[] { 6 }, new(first.Value.Version))).IsFailure.ShouldBeTrue();
        var old = new byte[3];
        await pinned.Stream.ReadExactlyAsync(old);
        old.ShouldBe(new byte[] { 1, 2, 3 });
        (await fixture.Provider.LookupAsync(fixture.Identity, new())).Value.Metadata.Version.ShouldBe(next.Value.Version.Value);
        var quota = (await fixture.Provider.GetQuotaAsync()).Value;
        quota.CurrentBytes.ShouldBe(2);
        quota.RetiredBytes.ShouldBe(3);
        quota.CurrentEntries.ShouldBe(1);
    }

    [Fact]
    public async Task StagingReaders_AreIndependentAndRemainPinnedUntilTheirActualDisposal()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var fence = (await fixture.Provider.CaptureFenceAsync(fixture.Identity)).Value;
        var opened = await fixture.Provider.BeginWriteAsync(new(fixture.Identity, fence, default, 4096));
        opened.IsSuccess.ShouldBeTrue();
        await using var session = opened.Value;
        var bytes = new byte[] { 1, 2, 3, 4 };
        await session.Stream.WriteAsync(bytes);
        var a = await session.OpenStagedReadAsync();
        var b = await session.OpenStagedReadAsync();
        a.IsSuccess.ShouldBeTrue();
        b.IsSuccess.ShouldBeTrue();
        await using var first = a.Value;
        await using var second = b.Value;
        first.Position = 2;
        first.ReadByte().ShouldBe(3);
        second.Position.ShouldBe(0);
        second.ReadByte().ShouldBe(1);
        await session.DisposeAsync();
        first.ReadByte().ShouldBe(4);
        second.ReadByte().ShouldBe(2);
        (await fixture.Provider.LookupAsync(fixture.Identity, new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
        await first.DisposeAsync();
        await second.DisposeAsync();
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, fence)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task NegativePublication_DoesNotCreateAnEmptyPositivePayloadFile()
    {
        await using var fixture = await Fixture.ReadyAsync(negative: true);
        var result = await fixture.WriteAsync([], notFound: true);

        result.IsSuccess.ShouldBeTrue();
        result.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        result.Value.Metadata.IsNotFound.ShouldBeTrue();
        Directory.Exists(Path.Combine(fixture.Root.ShardDirectory(fixture.Identity), "values")).ShouldBeFalse();
    }

    [Fact]
    public async Task CapturedAuthority_PersistsAcrossInstancesAndRepeatedReleaseIsHarmless()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var captured = await fixture.Provider.CaptureFenceAsync(fixture.Identity);
        captured.IsSuccess.ShouldBeTrue();
        Guid.TryParseExact(captured.Value.Value, "N", out _).ShouldBeTrue();
        await using var second = fixture.OtherProvider();
        (await second.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();

        (await second.ReleaseFenceAsync(fixture.Identity, captured.Value)).IsSuccess.ShouldBeTrue();
        (await second.ReleaseFenceAsync(fixture.Identity, captured.Value)).IsSuccess.ShouldBeTrue();
        (await fixture.Provider.LookupAsync(fixture.Identity, new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
        (await fixture.Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(0);
    }

    [Fact]
    public async Task ContractVersions_ShareLogicalFencingButKeepExactCaptureIdentitiesSeparate()
    {
        await using var fixture = new Fixture();
        var secondDefinition = fixture.Definition with { ContractVersion = "2", ContractId = "raw-v2" };
        (await fixture.Provider.InitializeAsync([fixture.Definition, secondDefinition])).IsSuccess.ShouldBeTrue();
        var secondIdentity = new CacheEntryIdentity(fixture.Identity.Namespace, fixture.Identity.Collection, "2", fixture.Identity.Key);
        var a = await fixture.Provider.CaptureFenceAsync(fixture.Identity);
        var b = await fixture.Provider.CaptureFenceAsync(secondIdentity);

        a.IsSuccess.ShouldBeTrue();
        b.IsSuccess.ShouldBeTrue();
        a.Value.ShouldNotBe(b.Value);
        (await fixture.Provider.ReleaseFenceAsync(secondIdentity, a.Value)).IsFailure.ShouldBeTrue();
        (await fixture.Provider.ReleaseFenceAsync(fixture.Identity, a.Value)).IsSuccess.ShouldBeTrue();
        (await fixture.Provider.ReleaseFenceAsync(secondIdentity, b.Value)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RepeatedSlidingReads_ReachButNeverExtendThePersistedAbsoluteCap()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var stored = await fixture.SeedAsync(new byte[] { 1 }, sliding: true);
        for (var index = 0; index < 6; index++)
        {
            fixture.Clock.Now = fixture.Clock.Now.AddSeconds(45);
            var acquired = await fixture.Provider.LookupAsync(fixture.Identity, new());
            acquired.IsSuccess.ShouldBeTrue();
            acquired.Value.State.ShouldBe(CacheReadValidationState.Valid);
            acquired.Value.Metadata.ExpiresAt.ShouldBeLessThanOrEqualTo(stored.AbsoluteExpiresAt);
        }

        fixture.Clock.Now = stored.AbsoluteExpiresAt.AddTicks(-1);
        (await fixture.Provider.LookupAsync(fixture.Identity, new())).Value.Metadata.ExpiresAt.ShouldBe(stored.AbsoluteExpiresAt);
        fixture.Clock.Now = stored.AbsoluteExpiresAt;
        (await fixture.Provider.LookupAsync(fixture.Identity, new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
    }

    [Fact]
    public async Task Construction_IsLazyAndReadinessPreservesCompatibleRetainedContracts()
    {
        await using var fixture = new Fixture();
        Directory.Exists(fixture.Path).ShouldBeFalse();
        (await fixture.Provider.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        (await fixture.Provider.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        await using var second = fixture.OtherProvider();
        (await second.InitializeAsync([fixture.Definition with { ContractVersion = "2", ContractId = "raw-v2" }])).IsSuccess.ShouldBeTrue();
        await using var incompatible = fixture.OtherProvider();
        var rejected = await incompatible.InitializeAsync([fixture.Definition with { ContractId = "changed" }]);

        rejected.IsFailure.ShouldBeTrue();
        rejected.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Compatibility);
        (await fixture.Provider.LookupAsync(fixture.Identity, new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
        Directory.Exists(fixture.Root.ShardDirectory(fixture.Identity)).ShouldBeFalse();
    }

    [Fact]
    public async Task MetadataAndChangedVersionReads_DoNotOpenOrPinPayloadFiles()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var metadata = await fixture.SeedAsync(new byte[] { 1, 2, 3 });
        var body = CachePayloadReference.PathFor(Path.Combine(fixture.Root.ShardDirectory(fixture.Identity), "values"), metadata.Version.Value);
        await using var exclusive = new FileStream(body, FileMode.Open, FileAccess.Read, FileShare.None);

        var acquired = await fixture.Provider.LookupAsync(fixture.Identity, new());
        var changed = await fixture.Provider.ValidateReadAsync(fixture.Identity, CacheEntryVersion.New(), new(OpenPayload: true));

        acquired.IsSuccess.ShouldBeTrue();
        acquired.Value.Metadata.Version.ShouldBe(metadata.Version);
        acquired.Value.Handle.ShouldBeNull();
        changed.IsSuccess.ShouldBeTrue();
        changed.Value.State.ShouldBe(CacheReadValidationState.Changed);
        changed.Value.Metadata.Version.ShouldBe(metadata.Version);
        changed.Value.Handle.ShouldBeNull();
        Directory.Exists(Path.Combine(fixture.Root.ShardDirectory(fixture.Identity), "pins")).ShouldBeFalse();
    }

    [Fact]
    public async Task AcquiredPayload_UsesTheSameVersionAndRemainsProtectedAfterProviderStopsAdmission()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var bytes = Enumerable.Range(0, 131111).Select(i => (byte)(i % 251)).ToArray();
        var metadata = await fixture.SeedAsync(bytes);
        var acquired = await fixture.Provider.LookupAsync(fixture.Identity, new(OpenPayload: true, RequireSeek: true));
        acquired.IsSuccess.ShouldBeTrue();
        await using var handle = acquired.Value.Handle;
        handle.Metadata.Version.ShouldBe(metadata.Version);
        await fixture.Provider.DisposeAsync();
        (await fixture.Provider.LookupAsync(fixture.Identity, new())).IsFailure.ShouldBeTrue();
        var range = (ICacheProviderRangeReadHandle)handle;
        (await range.PositionForRangeAsync(65530)).IsSuccess.ShouldBeTrue();
        var received = new byte[100];
        await handle.Stream.ReadExactlyAsync(received);

        received.ShouldBe(bytes[65530..65630]);
        Directory.EnumerateFiles(Path.Combine(fixture.Root.ShardDirectory(fixture.Identity), "pins"), "*.pin").Count().ShouldBe(1);
        await handle.DisposeAsync();
        Directory.EnumerateFiles(Path.Combine(fixture.Root.ShardDirectory(fixture.Identity), "pins"), "*.pin").ShouldBeEmpty();
    }

    [Fact]
    public async Task SlidingRead_RenewsWithinAbsoluteCapAndExpiryDoesNotServeOldPayload()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var stored = await fixture.SeedAsync(new byte[] { 1 }, sliding: true);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(30);
        var renewed = await fixture.Provider.LookupAsync(fixture.Identity, new());

        renewed.IsSuccess.ShouldBeTrue();
        renewed.Value.Metadata.Version.ShouldBe(stored.Version);
        renewed.Value.Metadata.ExpiresAt.ShouldBe(fixture.Clock.Now.AddMinutes(1));
        renewed.Value.Metadata.HasForegroundAccess.ShouldBeTrue();
        fixture.Clock.Now = stored.AbsoluteExpiresAt.AddTicks(-1);
        (await fixture.Provider.LookupAsync(fixture.Identity, new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
        (await fixture.Provider.TouchAsync(fixture.Identity)).Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.NotFound);
    }

    [Fact]
    public async Task NegativeMarker_ContainsNoPayloadAndBecomesAMissAtLogicalExpiry()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var stored = await fixture.SeedAsync([], notFound: true);
        var result = await fixture.Provider.LookupAsync(fixture.Identity, new(OpenPayload: true));

        result.IsSuccess.ShouldBeTrue();
        result.Value.State.ShouldBe(CacheReadValidationState.Valid);
        result.Value.Metadata.IsNotFound.ShouldBeTrue();
        result.Value.Handle.ShouldBeNull();
        Directory.Exists(Path.Combine(fixture.Root.ShardDirectory(fixture.Identity), "values")).ShouldBeFalse();
        fixture.Clock.Now = stored.ExpiresAt;
        (await fixture.Provider.LookupAsync(fixture.Identity, new(OpenPayload: true))).Value.State.ShouldBe(CacheReadValidationState.Missing);
    }

    [Fact]
    public async Task MissingCommittedPayload_FailsWithoutFallbackAndReleasesItsFailedOpenPin()
    {
        await using var fixture = await Fixture.ReadyAsync();
        var stored = await fixture.SeedAsync(new byte[] { 1 });
        File.Delete(CachePayloadReference.PathFor(Path.Combine(fixture.Root.ShardDirectory(fixture.Identity), "values"), stored.Version.Value));

        var acquired = await fixture.Provider.LookupAsync(fixture.Identity, new(OpenPayload: true));

        acquired.IsFailure.ShouldBeTrue();
        acquired.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Corruption);
        Directory.EnumerateFiles(Path.Combine(fixture.Root.ShardDirectory(fixture.Identity), "pins"), "*.pin").ShouldBeEmpty();
    }

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => this.Now;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ResourceBudget transient = new("fixture-transient", 32 * CacheDefaults.MiB);
        private readonly ResourceBudget resident = new("fixture-resident", 64 * CacheDefaults.MiB);
        private readonly CacheIndexBlockCache blocks = new(4 * CacheDefaults.MiB);

        internal Fixture(string durability = "Durable")
        {
            this.Options = new() { Kind = "FileSystem", RootPath = this.Path, MaxStorageBytes = 64 * CacheDefaults.MiB, Durability = durability };
            this.Root = new(new(this.Options), this.transient, this.resident);
            this.Provider = this.OtherProvider();
        }

        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cache-provider-" + Guid.NewGuid().ToString("N"));

        internal CacheEntryIdentity Identity { get; } = new("site", "items", "1", "key");

        internal Clock Clock { get; } = new();

        internal CacheProviderOptions Options { get; }

        internal CacheStoreRoot Root { get; }

        internal FileSystemCacheStoreProvider Provider { get; }

        internal CacheProviderCollection Definition { get; private set; } = new("site", "items", "1", "raw", [], new() { Limits = new() { MaxStorageBytes = 16 * CacheDefaults.MiB } });

        internal static async Task<Fixture> ReadyAsync(bool negative = false, TimeSpan? sourceTimeout = null, IReadOnlyList<CacheDimensionSchema> dimensions = null,
            string durability = "Durable", CacheExpirationOptions expiration = null)
        {
            var fixture = new Fixture(durability);
            if (negative) { fixture.Definition = fixture.Definition with { Policies = fixture.Definition.Policies with { NegativeCaching = new() { Enabled = true } } }; }

            if (sourceTimeout.HasValue) { fixture.Definition = fixture.Definition with { Policies = fixture.Definition.Policies with { Loading = fixture.Definition.Policies.Loading with { SourceTimeout = sourceTimeout.Value } } }; }

            if (dimensions is not null) { fixture.Definition = fixture.Definition with { Dimensions = dimensions }; }

            if (expiration is not null) { fixture.Definition = fixture.Definition with { Policies = fixture.Definition.Policies with { Expiration = expiration } }; }

            (await fixture.Provider.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
            return fixture;
        }

        internal FileSystemCacheStoreProvider OtherProvider() => new("files", this.Options, this.Clock);

        internal CacheOwnershipRequest Ownership(string node, string interest) => new(this.Identity, node, interest,
            this.Clock.Now.Add(this.Definition.Policies.Loading.SourceTimeout), this.Options.LeaseLifetime);

        internal async Task<Result<CacheWriteOutcome>> WriteOwnedAsync(byte[] bytes, CacheOwnershipObservation owner)
        {
            var begun = await this.Provider.BeginWriteAsync(new(this.Identity, owner.Fence, default, 4 * CacheDefaults.MiB, owner.Owner));
            if (begun.IsFailure) { return Result<CacheWriteOutcome>.Failure(begun); }

            await using var session = begun.Value;
            await session.Stream.WriteAsync(bytes);
            return await session.PublishAsync(this.Metadata(session, bytes, this.Identity));
        }

        internal async Task<Result<CacheWriteOutcome>> WriteAsync(byte[] bytes, CacheWriteCondition condition = default, bool notFound = false,
            string operationId = null, CacheEntryIdentity? identity = null, IReadOnlyList<CacheDimension> dimensions = null)
        {
            var target = identity ?? this.Identity;
            var fence = await this.Provider.CaptureFenceAsync(target);
            fence.IsSuccess.ShouldBeTrue();
            try
            {
                var begun = await this.Provider.BeginWriteAsync(new(target, fence.Value, condition, 4 * CacheDefaults.MiB, OperationId: operationId));
                if (begun.IsFailure) { return Result<CacheWriteOutcome>.Failure(begun); }

                await using var session = begun.Value;
                if (!notFound) { await session.Stream.WriteAsync(bytes); }

                return await session.PublishAsync(this.Metadata(session, bytes, target, notFound) with { Dimensions = dimensions ?? [] });
            }
            finally { (await this.Provider.ReleaseFenceAsync(target, fence.Value)).IsSuccess.ShouldBeTrue(); }
        }

        internal CacheEntryMetadata Metadata(ICacheProviderWriteSession session, byte[] bytes, CacheEntryIdentity target, bool notFound = false)
        {
            var expires = this.Clock.Now.Add(notFound ? TimeSpan.FromSeconds(15) : TimeSpan.FromMinutes(5));
            return new() { Identity = target, Version = session.Version, ContractId = "RAW", CodecId = "RAW",
                PublishedAt = this.Clock.Now, ExpiresAt = expires, AbsoluteExpiresAt = expires, IsNotFound = notFound,
                LogicalSizeBytes = bytes.Length, EncodedSizeBytes = bytes.Length, StoredSizeBytes = bytes.Length,
                StoredHash = notFound ? null : Convert.ToHexStringLower(SHA256.HashData(bytes)),
                PayloadHash = notFound ? null : Convert.ToHexStringLower(SHA256.HashData(bytes)) };
        }

        internal async Task<CacheEntryMetadata> SeedAsync(byte[] bytes, bool sliding = false, bool notFound = false)
        {
            using var header = await this.Root.OpenAsync(default);
            var ledger = new CacheRootQuotaLedger(this.Root, header.Value, header.Bytes, this.transient, this.resident, this.blocks, this.Clock);
            var shard = new CacheShardStore(this.Root, this.Root.ShardDirectory(this.Identity), header.Value.StoreId, this.Identity.CollectionIdentity,
                CacheStoreRoot.Shard(this.Identity), 0, this.transient, this.resident, this.blocks);
            var version = CacheEntryVersion.New();
            await using var mutation = await CacheNativeMutation.CreateAsync(ledger, shard, this.Identity, "fixture", version.Value,
                Guid.NewGuid().ToString("N"), this.transient, this.resident, default);
            await using (var initialized = await shard.AcquireAsync(true, token => ledger.GetEpochAsync(this.Identity.CollectionIdentity, token), mutation.ReserveAsync, default))
            {
                initialized.Manifest.Sequence.ShouldBe(0);
            }

            CachePayloadReference payload = null;
            if (!notFound)
            {
                await using var writer = await CachePayloadWriter.CreateAsync(System.IO.Path.Combine(shard.DirectoryPath, "values"), header.Value.StoreId,
                    version.Value, 4 * CacheDefaults.MiB, this.Root.Profile,
                    (needed, token) => mutation.ReserveAsync(needed + 8192, token), this.transient, this.resident, default);
                await writer.WriteAsync(bytes);
                payload = await writer.SealAsync(default);
            }

            var metadata = new CacheEntryMetadata { Identity = this.Identity, Version = version, ContractId = "RAW", IsNotFound = notFound,
                PublishedAt = this.Clock.Now, ExpiresAt = this.Clock.Now.AddMinutes(1), AbsoluteExpiresAt = this.Clock.Now.AddMinutes(5),
                SlidingIdleTimeout = sliding ? TimeSpan.FromMinutes(1) : null, StoredSizeBytes = bytes.Length, EncodedSizeBytes = bytes.Length,
                LogicalSizeBytes = bytes.Length, StoredHash = payload?.Hash ?? Convert.ToHexStringLower(SHA256.HashData(bytes)) };
            await using var transaction = await shard.AcquireAsync(true, token => ledger.GetEpochAsync(this.Identity.CollectionIdentity, token), mutation.ReserveAsync, default);
            var entry = new CacheNativeEntry(1, metadata, payload, mutation.CreditId, mutation.Commit.Generation, transaction.NextSequence, 0, 0,
                metadata.ExpiresAt.Add(this.Definition.Policies.Expiration.RetentionGrace));
            CacheNativeRecords.Set(transaction, CacheMetadataIndex.Primary(this.Identity), entry, this.transient, this.resident);
            var pointer = CacheMetadataIndex.Pointer(metadata);
            foreach (var key in CacheMetadataIndex.Keys(metadata, entry.PurgeAfter)) { transaction.Set(key, pointer); }

            var stage = (payload?.PhysicalBytes ?? 0) + 4096;
            await mutation.CommitAsync(transaction, 0, new(CurrentBytes: bytes.Length,
                MetadataBytes: transaction.EstimateCommitGrowth(mutation.Commit, mutation.OperationId) + 4096 + (payload?.PhysicalBytes ?? 0) - bytes.Length,
                Entries: 1), default, existingStageBytes: stage);
            return metadata;
        }

        public async ValueTask DisposeAsync()
        {
            await this.Provider.DisposeAsync();
            this.blocks.Dispose();
            if (Directory.Exists(this.Path)) { Directory.Delete(this.Path, recursive: true); }

            this.transient.Used.ShouldBe(0);
            this.resident.Used.ShouldBe(0);
        }
    }
}