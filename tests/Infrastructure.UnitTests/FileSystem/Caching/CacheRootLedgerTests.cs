// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

public class CacheRootLedgerTests
{
    [Theory]
    [InlineData("Durable")]
    [InlineData("Buffered")]
    public async Task DurableReceipt_RejectsAnOlderInternallyValidDataManifestInsteadOfServingIt(string durability)
    {
        await using var fixture = await Fixture.CreateAsync(durability: durability);
        var ledger = fixture.Ledger();
        await ledger.RegisterCollectionAsync(fixture.Definition, default);
        CacheManifest old;
        await using (var first = await fixture.MutationAsync(ledger))
        await using (var transaction = await fixture.AcquireShardAsync(first.ReserveAsync))
        {
            transaction.Set(new(new byte[] { 16, 1 }), "old"u8);
            old = await first.CommitAsync(transaction, 0, new(MetadataBytes: 120000), default);
        }

        await using (var next = await fixture.MutationAsync(ledger))
        await using (var transaction = await fixture.AcquireShardAsync(next.ReserveAsync))
        {
            transaction.Set(new(new byte[] { 16, 1 }), "new"u8);
            var current = await next.CommitAsync(transaction, 0, new(MetadataBytes: 120000), default);
            (await ledger.GetReceiptAsync(fixture.Identity, default)).Verify(current);
            // Simulate loss/reappearance of an older, completely checksummed data generation.
            await fixture.RestoreManifestForFaultAsync(old, transaction.Authority);
        }

        await using var acquired = await fixture.AcquireShardAsync((_, _) => ValueTask.FromResult(Result.Success()));
        var receipt = await fixture.Ledger().GetReceiptAsync(fixture.Identity, default);
        receipt.State.ShouldBe(CacheShardReceiptState.Committed);
        Should.Throw<CacheTransferException>(() => receipt.Verify(acquired.Manifest)).Code.ShouldBe(CacheErrorCode.ProviderUnavailable);
    }

    [Fact]
    public async Task NativeMutation_UsesOnePersistedCreditAndFinalizesAroundItsActualShardPublication()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ledger = fixture.Ledger();
        await ledger.RegisterCollectionAsync(fixture.Definition, default);
        await using var mutation = await fixture.MutationAsync(ledger);
        await using (var shard = await fixture.AcquireShardAsync(mutation.ReserveAsync))
        {
            shard.Set(new(new byte[] { 16, 1 }), "current"u8);
            await mutation.CommitAsync(shard, 0, new(MetadataBytes: 120000), default);
        }

        var credit = await fixture.Ledger().GetCreditAsync(mutation.CreditId, default);
        credit.State.ShouldBe(CacheSpaceCreditState.Committed);
        credit.Commit.ShouldBe(mutation.Commit);
        (await ledger.GetQuotaAsync(default)).MetadataBytes.ShouldBeGreaterThanOrEqualTo(120000);
        await using var current = await fixture.AcquireShardAsync(mutation.ReserveAsync);
        current.Manifest.Generation.ShouldBe(mutation.Commit.Generation);
        current.Manifest.OperationId.ShouldBe(mutation.OperationId);
    }

    [Fact]
    public async Task NativeMutation_RootEpochRevocationBetweenStagingAndPublicationKeepsTheNewIndexInvisible()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ledger = fixture.Ledger();
        await ledger.RegisterCollectionAsync(fixture.Definition, default);
        await using var mutation = await fixture.MutationAsync(ledger);
        fixture.Shard.Fault = boundary =>
        {
            if (boundary == "index-staged") { ledger.AdvanceEpochAsync(fixture.Identity.CollectionIdentity, default).AsTask().GetAwaiter().GetResult(); }
        };
        await using (var shard = await fixture.AcquireShardAsync(mutation.ReserveAsync))
        {
            shard.Set(new(new byte[] { 16, 1 }), "rejected"u8);
            (await Should.ThrowAsync<CacheTransferException>(async () => await mutation.CommitAsync(shard, 0, new(MetadataBytes: 120000), default)))
                .Code.ShouldBe(CacheErrorCode.Conflict);
        }

        (await ledger.GetCreditAsync(mutation.CreditId, default)).State.ShouldBe(CacheSpaceCreditState.Reserved);
        await using var current = await fixture.AcquireShardAsync(mutation.ReserveAsync);
        current.Manifest.Sequence.ShouldBe(0);
        (await current.ReadAsync(new(new byte[] { 16, 1 }), default)).ShouldBeNull();
    }

    [Fact]
    public async Task NativeMutation_ManifestAcknowledgementLossLeavesPreparedQuotaForExactNativeReconciliation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ledger = fixture.Ledger();
        await ledger.RegisterCollectionAsync(fixture.Definition, default);
        await using var mutation = await fixture.MutationAsync(ledger);
        fixture.Shard.Fault = boundary => { if (boundary == "manifest-published") { throw new IOException("Simulated acknowledgement loss."); } };
        await using (var shard = await fixture.AcquireShardAsync(mutation.ReserveAsync))
        {
            shard.Set(new(new byte[] { 16, 1 }), "committed"u8);
            (await Should.ThrowAsync<CacheTransferException>(async () => await mutation.CommitAsync(shard, 0, new(MetadataBytes: 120000), default)))
                .Code.ShouldBe(CacheErrorCode.Indeterminate);
        }

        (await ledger.GetCreditAsync(mutation.CreditId, default)).State.ShouldBe(CacheSpaceCreditState.Prepared);
        await using var current = await fixture.AcquireShardAsync(mutation.ReserveAsync);
        current.Manifest.Generation.ShouldBe(mutation.Commit.Generation);
        var prepared = await ledger.GetReceiptAsync(fixture.Identity, default);
        prepared.State.ShouldBe(CacheShardReceiptState.Prepared);
        Should.Throw<CacheTransferException>(() => prepared.Verify(current.Manifest)).Code.ShouldBe(CacheErrorCode.ProviderUnavailable);
        var confirmed = await ledger.ConfirmPreparedAsync(mutation.CreditId, fixture.Identity, null, current.Manifest.Generation, default);
        confirmed.State.ShouldBe(CacheSpaceCreditState.Committed);
        (await ledger.GetReceiptAsync(fixture.Identity, default)).Verify(current.Manifest);
    }

    [Fact]
    public async Task NativeMutation_DisposalWaitsForTheActualPublicationTailBeforeReleasingOwnerAndMemory()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ledger = fixture.Ledger();
        await ledger.RegisterCollectionAsync(fixture.Definition, default);
        await using var mutation = await fixture.MutationAsync(ledger);
        await using var shard = await fixture.AcquireShardAsync(mutation.ReserveAsync);
        shard.Set(new(new byte[] { 16, 1 }), "committed"u8);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finish = new ManualResetEventSlim();
        fixture.Shard.Fault = boundary =>
        {
            if (boundary == "manifest-published")
            {
                entered.SetResult();
                if (!finish.Wait(TimeSpan.FromSeconds(10))) { throw new TimeoutException("The controlled publication tail was not released."); }
            }
        };
        var publishing = Task.Run(async () => await mutation.CommitAsync(shard, 0, new(MetadataBytes: 120000), default));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var disposing = mutation.DisposeAsync().AsTask();
            disposing.IsCompleted.ShouldBeFalse();
            using var competing = FileSystemAuthority.TryAcquire(fixture.Profile.AuthorityOptions, mutation.OwnerDirectory, "stage-mutate-" + mutation.OperationId);
            competing.ShouldBeNull();
            finish.Set();
            await publishing;
            await disposing;
        }
        finally { finish.Set(); await publishing; await mutation.DisposeAsync(); }
    }

    [Fact]
    public async Task NativeCatalog_RetainsOldContractsAndRejectsChangingAnExistingValueAliasOrDimensionSchema()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ledger = fixture.Ledger();
        var original = fixture.Definition with { ContractId = "item", Dimensions = [new("region", CacheDimensionKind.String)] };
        await ledger.RegisterCollectionAsync(original, default);
        await ledger.RegisterContractAsync(original, default);
        var next = original with { ContractVersion = "2", ContractId = "item-v2", Dimensions = [new("region", CacheDimensionKind.Identifier)] };
        await fixture.Ledger().RegisterContractAsync(next, default);

        (await fixture.Ledger().GetContractAsync(fixture.Identity, default)).Dimensions.Single().Kind.ShouldBe(CacheDimensionKind.String);
        (await fixture.Ledger().GetContractAsync(new("site", "items", "2", "other"), default)).Dimensions.Single().Kind.ShouldBe(CacheDimensionKind.Identifier);
        (await Should.ThrowAsync<CacheTransferException>(async () => await ledger.RegisterContractAsync(original with { ContractId = "changed" }, default)))
            .Code.ShouldBe(CacheErrorCode.Compatibility);
        (await Should.ThrowAsync<CacheTransferException>(async () => await ledger.RegisterContractAsync(original with { Dimensions = next.Dimensions }, default)))
            .Code.ShouldBe(CacheErrorCode.Compatibility);
        var repeated = await ledger.RegisterContractAsync(original with { Namespace = "SITE", Collection = "ITEMS", ContractId = "ITEM" }, default);
        repeated.Identity.Collection.ShouldBe("ITEMS");
    }

    [Fact]
    public async Task CursorSigningKey_PersistsAcrossIndependentInstancesAndIsReturnedAsAnOwnedCopy()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.Ledger().GetCursorKeyAsync(default);
        var expected = first.ToArray();
        Array.Fill(first, (byte)0);

        var second = await fixture.Ledger().GetCursorKeyAsync(default);

        second.Length.ShouldBe(32);
        second.ShouldBe(expected);
        second.ShouldNotBe(first);
    }

    [Fact]
    public async Task Publication_PreparesAndFinalizesQuotaAroundItsSingleNamespaceCallback()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ledger = fixture.Ledger();
        await ledger.RegisterCollectionAsync(fixture.Definition, default);
        var credit = fixture.Credit();
        await ledger.ReserveAsync(credit, 1000, default);
        var commit = new CacheShardCommitIdentity(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        await ledger.AttachCommitAsync(credit.Id, credit.NodeId, commit, default);
        var calls = 0;

        var acknowledged = await ledger.PublishAsync(credit.Id, credit.NodeId, 0, new(CurrentBytes: 200, MetadataBytes: 50, Entries: 1), _ =>
        {
            calls++;
            return ValueTask.FromResult(123L);
        }, default);

        acknowledged.ShouldBe(123);
        calls.ShouldBe(1);
        var confirmed = await ledger.GetCreditAsync(credit.Id, default);
        confirmed.State.ShouldBe(CacheSpaceCreditState.Committed);
        confirmed.ReservedBytes.ShouldBe(0);
        var quota = await ledger.GetQuotaAsync(default);
        quota.CurrentBytes.ShouldBe(200);
        quota.CurrentEntries.ShouldBe(1);
    }

    [Fact]
    public async Task LostPublicationAcknowledgement_RetainsPreparedCreditsUntilNativeProofConfirmsThem()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ledger = fixture.Ledger();
        await ledger.RegisterCollectionAsync(fixture.Definition, default);
        var credit = fixture.Credit();
        await ledger.ReserveAsync(credit, 1000, default);
        var commit = new CacheShardCommitIdentity(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        await ledger.AttachCommitAsync(credit.Id, credit.NodeId, commit, default);

        await Should.ThrowAsync<IOException>(async () => await ledger.PublishAsync(credit.Id, credit.NodeId, 0,
            new(CurrentBytes: 200, Entries: 1), _ => throw new IOException("Simulated namespace acknowledgement loss."), default));

        var pending = await fixture.Ledger().GetCreditAsync(credit.Id, default);
        pending.State.ShouldBe(CacheSpaceCreditState.Prepared);
        pending.ReservedBytes.ShouldBe(1000);
        var quota = await ledger.GetQuotaAsync(default);
        quota.CurrentEntries.ShouldBe(1); // Conservative admission count includes the unresolved publication.
        quota.IsExact.ShouldBeFalse();
        var confirmed = await ledger.ConfirmPreparedAsync(credit.Id, credit.Identity, credit.Version, commit.Generation, default);
        confirmed.State.ShouldBe(CacheSpaceCreditState.Committed);
        (await ledger.GetQuotaAsync(default)).CurrentBytes.ShouldBe(200);
        (await ledger.ConfirmPreparedAsync(credit.Id, credit.Identity, credit.Version, commit.Generation, default)).State.ShouldBe(CacheSpaceCreditState.Committed);
    }

    [Fact]
    public async Task RevokedCollectionEpoch_RejectsBeforePublicationAndAllowsProvenUnpublishedCleanup()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ledger = fixture.Ledger();
        await ledger.RegisterCollectionAsync(fixture.Definition, default);
        var credit = fixture.Credit();
        await ledger.ReserveAsync(credit, 1000, default);
        await ledger.AttachCommitAsync(credit.Id, credit.NodeId, new(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")), default);
        await ledger.AdvanceEpochAsync(credit.Identity.CollectionIdentity, default);
        var called = false;

        var rejected = await Should.ThrowAsync<CacheTransferException>(async () => await ledger.PublishAsync(credit.Id, credit.NodeId, 0, new(Entries: 1), _ =>
        {
            called = true;
            return ValueTask.FromResult(1L);
        }, default));
        await ledger.ReleaseUnpublishedAsync(credit.Id, credit.NodeId, default);

        rejected.Code.ShouldBe(CacheErrorCode.Conflict);
        called.ShouldBeFalse();
        (await ledger.GetCreditAsync(credit.Id, default)).State.ShouldBe(CacheSpaceCreditState.Released);
        (await ledger.GetQuotaAsync(default)).CurrentEntries.ShouldBe(0);
    }

    [Fact]
    public async Task IndependentInstances_ContendForOnePersistentCollectionQuota()
    {
        await using var fixture = await Fixture.CreateAsync(128 * CacheDefaults.KiB);
        var first = fixture.Ledger();
        var second = fixture.Ledger();
        await first.RegisterCollectionAsync(fixture.Definition, default);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var a = Task.Run(async () => { await start.Task; return await TryReserveAsync(first, fixture.Credit(), 100 * CacheDefaults.KiB); });
        var b = Task.Run(async () => { await start.Task; return await TryReserveAsync(second, fixture.Credit(), 100 * CacheDefaults.KiB); });

        start.SetResult();
        var results = await Task.WhenAll(a, b);

        results.Count(r => r).ShouldBe(1);
        (await first.GetQuotaAsync(default)).StagingBytes.ShouldBeGreaterThanOrEqualTo(100 * CacheDefaults.KiB);
        (await second.GetEpochAsync(fixture.Identity.CollectionIdentity, default)).ShouldBe(0);
        (await first.AdvanceEpochAsync(fixture.Identity.CollectionIdentity, default)).ShouldBe(1);
        (await second.GetEpochAsync(fixture.Identity.CollectionIdentity, default)).ShouldBe(1);
    }

    [Fact]
    public async Task Credits_PersistAcrossInstancesAndRepeatedAdmissionDoesNotDoubleCharge()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ledger = fixture.Ledger();
        await ledger.RegisterCollectionAsync(fixture.Definition, default);
        var requested = fixture.Credit();
        var first = await ledger.ReserveAsync(requested, 65536, default);
        var before = await ledger.GetQuotaAsync(default);

        var peer = fixture.Ledger();
        var repeated = await peer.ReserveAsync(requested, 65536, default);
        var retained = await peer.GetCreditAsync(requested.Id, default);

        repeated.ReservedBytes.ShouldBe(first.ReservedBytes);
        retained.Identity.ShouldBe(fixture.Identity);
        retained.Version.ShouldBe(requested.Version);
        var after = await peer.GetQuotaAsync(default);
        (after.CurrentBytes + after.RetiredBytes + after.StagingBytes + after.MetadataBytes)
            .ShouldBe(before.CurrentBytes + before.RetiredBytes + before.StagingBytes + before.MetadataBytes);
        (await peer.GrowAsync(requested.Id, requested.NodeId, 131072, default)).ReservedBytes.ShouldBe(131072);
        (await Should.ThrowAsync<CacheTransferException>(async () => await peer.GrowAsync(requested.Id, "other", 196608, default))).Code.ShouldBe(CacheErrorCode.Conflict);
    }

    [Fact]
    public async Task ControlLedgerCheckpoint_KeepsCreditRecordsAndCountersWhileBoundingMetadataGenerations()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ledger = fixture.Ledger();
        await ledger.RegisterCollectionAsync(fixture.Definition, default);
        var credit = fixture.Credit();
        await ledger.ReserveAsync(credit, 1, default);

        for (var i = 2; i < 70; i++) { await ledger.GrowAsync(credit.Id, credit.NodeId, i, default); }

        (await fixture.Ledger().GetCreditAsync(credit.Id, default)).ReservedBytes.ShouldBe(69);
        var quota = await ledger.GetQuotaAsync(default);
        (quota.CurrentBytes + quota.RetiredBytes + quota.StagingBytes + quota.MetadataBytes).ShouldBeLessThanOrEqualTo(quota.MaximumBytes);
        Directory.EnumerateFiles(Path.Combine(fixture.Path, "ledger"), "*.idx").Count().ShouldBeLessThan(32);
        File.Exists(Path.Combine(fixture.Path, "ledger", "intent.json")).ShouldBeFalse();
    }

    private static async Task<bool> TryReserveAsync(CacheRootQuotaLedger ledger, CacheSpaceCredit credit, long bytes)
    {
        try { await ledger.ReserveAsync(credit, bytes, default); return true; }
        catch (CacheTransferException exception) when (exception.Code == CacheErrorCode.Capacity) { return false; }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ResourceBudget transient = new("transient", 32 * CacheDefaults.MiB);
        private readonly ResourceBudget resident = new("resident", 64 * CacheDefaults.MiB);
        private readonly CacheIndexBlockCache blocks = new(4 * CacheDefaults.MiB);
        private CacheStoreRoot root;
        private CacheStoreHeader header;
        private long rootBytes;

        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cache-ledger-" + Guid.NewGuid().ToString("N"));

        internal CacheEntryIdentity Identity { get; } = new("site", "items", "1", "key");

        internal CacheProviderCollection Definition { get; private set; }

        internal CacheShardStore Shard { get; private set; }

        internal FileSystemCacheOptions Profile => this.root.Profile;

        internal static async Task<Fixture> CreateAsync(long collectionMaximum = 16 * CacheDefaults.MiB, string durability = "Durable")
        {
            var fixture = new Fixture();
            fixture.root = new(new(new() { RootPath = fixture.Path, MaxStorageBytes = 64 * CacheDefaults.MiB, Durability = durability }), fixture.transient, fixture.resident);
            using var header = await fixture.root.OpenAsync(default);
            fixture.header = header.Value;
            fixture.rootBytes = header.Bytes;
            fixture.Definition = new("site", "items", "1", "raw", [], new() { Limits = new() { MaxStorageBytes = collectionMaximum } });
            fixture.Shard = new(fixture.root, fixture.root.ShardDirectory(fixture.Identity), fixture.header.StoreId, fixture.Identity.CollectionIdentity,
                CacheStoreRoot.Shard(fixture.Identity), 0, fixture.transient, fixture.resident, fixture.blocks);
            return fixture;
        }

        internal CacheRootQuotaLedger Ledger() => new(this.root, this.header, this.rootBytes, this.transient, this.resident, this.blocks, TimeProvider.System);

        internal ValueTask<CacheNativeMutation> MutationAsync(CacheRootQuotaLedger ledger) => CacheNativeMutation.CreateAsync(ledger, this.Shard,
            this.Identity, "node", null, Guid.NewGuid().ToString("N"), this.transient, this.resident, default);

        internal ValueTask<CacheShardTransaction> AcquireShardAsync(Func<long, CancellationToken, ValueTask<Result>> reserve) => this.Shard.AcquireAsync(true,
            _ => this.Ledger().GetEpochAsync(this.Identity.CollectionIdentity, default), reserve, default);

        internal ValueTask<long> RestoreManifestForFaultAsync(CacheManifest manifest, FileSystemAuthority authority) => CacheCheckedMetadata.WriteAsync(
            this.Shard.DirectoryPath, "manifest.json", manifest, authority, true, this.transient, this.resident, default);

        internal CacheSpaceCredit Credit()
        {
            var operation = Guid.NewGuid().ToString("N");
            return new() { Id = operation, OperationId = operation, NodeId = "node", Owner = "stage-" + Guid.NewGuid().ToString("N"),
                Identity = this.Identity, Version = Guid.NewGuid().ToString("N") };
        }

        public ValueTask DisposeAsync()
        {
            this.blocks.Dispose();
            Directory.Delete(this.Path, recursive: true);
            this.transient.Used.ShouldBe(0);
            this.resident.Used.ShouldBe(0);
            return ValueTask.CompletedTask;
        }
    }
}