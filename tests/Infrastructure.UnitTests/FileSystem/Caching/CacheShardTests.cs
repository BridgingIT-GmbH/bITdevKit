// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

public class CacheShardTests
{
    [Fact]
    public async Task StagedCheckpoint_ProtectsItsInputsAndPublishesOneCompleteMergedGeneration()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using (var first = await fixture.AcquireAsync())
        {
            first.Set(Key(1), "old"u8);
            first.Set(Key(2), "deleted"u8);
            await first.CommitAsync(Guid.NewGuid().ToString("N"), ReserveAsync, null, default);
        }

        await using (var next = await fixture.AcquireAsync())
        {
            next.Set(Key(1), "current"u8);
            next.Set(Key(2), [], deleted: true);
            await next.CommitAsync(Guid.NewGuid().ToString("N"), ReserveAsync, null, default);
        }

        var checkpoint = await fixture.CheckpointAsync(ReserveAsync);
        try
        {
            await using (var current = await fixture.AcquireAsync())
            {
                (await fixture.Pins.IsPinnedAsync(current.Authority, "index", checkpoint.Input.Segments[0].Id, default)).ShouldBeTrue();
                current.Manifest.Segments.Count.ShouldBe(2);
            }

            await checkpoint.PublishAsync(null, default);
            await using var compacted = await fixture.AcquireAsync();
            compacted.Manifest.Sequence.ShouldBe(3);
            compacted.Manifest.Segments.Count.ShouldBe(1);
            compacted.Manifest.JournalBytes.ShouldBe(0);
            compacted.Manifest.CheckpointSequence.ShouldBe(3);
            using var value = await compacted.ReadAsync(Key(1), default);
            value.Bytes.ToArray().ShouldBe("current"u8.ToArray());
            (await compacted.ReadAsync(Key(2), default)).ShouldBeNull();
            File.Exists(Path.Combine(fixture.Shard.DirectoryPath, checkpoint.Input.Journal + ".journal")).ShouldBeTrue();
        }
        finally { await checkpoint.DisposeAsync(); }

        await using var released = await fixture.AcquireAsync();
        (await fixture.Pins.IsPinnedAsync(released.Authority, "index", checkpoint.Input.Segments[0].Id, default)).ShouldBeFalse();
    }

    [Fact]
    public async Task CheckpointMerge_ReleasesShardAuthorityAndLosesItsGenerationCompareIfAWriterPublishes()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using (var first = await fixture.AcquireAsync())
        {
            first.Set(Key(1), "initial"u8);
            await first.CommitAsync(Guid.NewGuid().ToString("N"), ReserveAsync, null, default);
        }

        var merging = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueMerge = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preparing = fixture.CheckpointAsync(async (bytes, token) =>
        {
            if (bytes > 8192) { merging.TrySetResult(); await continueMerge.Task.WaitAsync(token); }

            return Result.Success();
        }).AsTask();
        await merging.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await using (var writer = await fixture.AcquireAsync())
        {
            writer.Set(Key(1), "concurrent"u8);
            await writer.CommitAsync(Guid.NewGuid().ToString("N"), ReserveAsync, null, default);
        }

        continueMerge.SetResult();
        await using var checkpoint = await preparing;
        (await Should.ThrowAsync<CacheTransferException>(async () => await checkpoint.PublishAsync(null, default))).Code.ShouldBe(CacheErrorCode.Conflict);
        await using var current = await fixture.AcquireAsync();
        current.Manifest.Segments.Count.ShouldBe(2);
        using var value = await current.ReadAsync(Key(1), default);
        value.Bytes.ToArray().ShouldBe("concurrent"u8.ToArray());
    }

    [Fact]
    public async Task LostCheckpointAcknowledgement_IsIndeterminateAndNeverRollsBackThePublishedGeneration()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using (var first = await fixture.AcquireAsync())
        {
            first.Set(Key(1), "retained"u8);
            await first.CommitAsync(Guid.NewGuid().ToString("N"), ReserveAsync, null, default);
        }

        await using var checkpoint = await fixture.CheckpointAsync(ReserveAsync);
        var failure = await Should.ThrowAsync<CacheTransferException>(async () => await checkpoint.PublishAsync(
            async (publish, token) => { await publish(token); throw new IOException("Simulated checkpoint acknowledgement loss."); }, default));

        failure.Code.ShouldBe(CacheErrorCode.Indeterminate);
        await using var current = await fixture.AcquireAsync();
        current.Manifest.Generation.ShouldBe(checkpoint.Output.Generation);
        current.Manifest.OperationId.ShouldBe(checkpoint.Output.OperationId);
    }

    [Fact]
    public async Task RootStylePreparationAndFinalization_KeepOneAuthorityAndRefreshOnlyConfirmedSnapshots()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var transaction = await fixture.AcquireAsync();
        transaction.Set(Key(1), "prepared"u8);
        await transaction.CommitAsync(Guid.NewGuid().ToString("N"), ReserveAsync, null, default);

        await transaction.RefreshAfterCommitAsync(default);
        using (var prepared = await transaction.ReadAsync(Key(1), default)) { prepared.Bytes.ToArray().ShouldBe("prepared"u8.ToArray()); }

        transaction.Set(Key(1), "committed"u8);
        transaction.Set(Key(2), "other"u8);
        await transaction.CommitAsync(Guid.NewGuid().ToString("N"), ReserveAsync, null, default);
        await transaction.RefreshAfterCommitAsync(default);
        var selected = new List<byte[]>();
        CacheIndexKey last = null;
        await foreach (var row in transaction.ScanAsync(Key(1), 1, default)) { selected.Add(row.Bytes.ToArray()); last = row.Key; }

        selected.Single().ShouldBe("committed"u8.ToArray());
        await foreach (var row in transaction.ScanAsync(last.After(), 100, default)) { selected.Add(row.Bytes.ToArray()); }

        selected.Count.ShouldBe(2);
        selected[1].ShouldBe("other"u8.ToArray());
        transaction.Manifest.Sequence.ShouldBe(2);
    }

    [Fact]
    public async Task CommittedIndexWatermark_ReadsCurrentMetadataWithoutScanningItsJournal()
    {
        await using var fixture = await Fixture.CreateAsync();
        CacheManifest committed;
        await using (var transaction = await fixture.AcquireAsync())
        {
            transaction.Set(Key(1), "current"u8);
            transaction.Set(Key(2), "other"u8);
            committed = await transaction.CommitAsync(Guid.NewGuid().ToString("N"), ReserveAsync, null, default);
        }

        // Journal validation belongs to recovery. Ordinary reads use only the manifest's immutable point indexes.
        await using (var journal = new FileStream(Path.Combine(fixture.Shard.DirectoryPath, committed.Journal + ".journal"), FileMode.Open, FileAccess.Write))
        {
            journal.WriteByte(0xff);
        }

        await using (var transaction = await fixture.AcquireAsync())
        {
            using var value = await transaction.ReadAsync(Key(1), default);
            value.Bytes.ToArray().ShouldBe("current"u8.ToArray());
            transaction.Manifest.Generation.ShouldBe(committed.Generation);
        }
    }

    [Fact]
    public async Task FinalizationGuard_RejectionKeepsThePreviouslyCommittedVersionVisible()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using (var first = await fixture.AcquireAsync())
        {
            first.Set(Key(1), "old"u8);
            await first.CommitAsync(Guid.NewGuid().ToString("N"), ReserveAsync, null, default);
        }

        await using (var rejected = await fixture.AcquireAsync())
        {
            rejected.Set(Key(1), "rejected"u8);
            var error = await Should.ThrowAsync<CacheTransferException>(async () => await rejected.CommitAsync(Guid.NewGuid().ToString("N"), ReserveAsync,
                (_, _) => throw new CacheTransferException(CacheErrorCode.Conflict), default));
            error.Code.ShouldBe(CacheErrorCode.Conflict);
        }

        await using var current = await fixture.AcquireAsync();
        using var value = await current.ReadAsync(Key(1), default);
        value.Bytes.ToArray().ShouldBe("old"u8.ToArray());
        current.Set(Key(1), "next"u8);
        await current.CommitAsync(Guid.NewGuid().ToString("N"), ReserveAsync, null, default);
    }

    [Fact]
    public async Task LostFinalizationAcknowledgement_IsIndeterminateAndKeepsItsExactCommittedOperationIdentity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var operation = Guid.NewGuid().ToString("N");
        await using (var transaction = await fixture.AcquireAsync())
        {
            transaction.Set(Key(1), "published"u8);
            var error = await Should.ThrowAsync<CacheTransferException>(async () => await transaction.CommitAsync(operation, ReserveAsync,
                async (publish, token) => { await publish(token); throw new IOException("Simulated acknowledgement loss."); }, default));
            error.Code.ShouldBe(CacheErrorCode.Indeterminate);
        }

        await using var current = await fixture.AcquireAsync();
        current.Manifest.OperationId.ShouldBe(operation);
        using var value = await current.ReadAsync(Key(1), default);
        value.Bytes.ToArray().ShouldBe("published"u8.ToArray());
    }

    [Fact]
    public async Task MissingCommittedManifest_IsCorruptionAndCannotReinitializeAnEmptyShard()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using (var transaction = await fixture.AcquireAsync())
        {
            transaction.Set(Key(1), "value"u8);
            await transaction.CommitAsync(Guid.NewGuid().ToString("N"), ReserveAsync, null, default);
        }

        File.Delete(Path.Combine(fixture.Shard.DirectoryPath, "manifest.json"));

        (await Should.ThrowAsync<CacheTransferException>(async () => await fixture.AcquireAsync())).Code.ShouldBe(CacheErrorCode.Corruption);
        File.Exists(Path.Combine(fixture.Shard.DirectoryPath, "manifest.json")).ShouldBeFalse();
    }

    private static CacheIndexKey Key(byte value) => new(new byte[] { 1, value });

    private static ValueTask<Result> ReserveAsync(long _, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result.Success());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "cache-shard-" + Guid.NewGuid().ToString("N"));
        private readonly ResourceBudget transient = new("transient", 16 * CacheDefaults.MiB);
        private readonly ResourceBudget resident = new("resident", 16 * CacheDefaults.MiB);
        private readonly CacheIndexBlockCache blocks = new(4 * CacheDefaults.MiB);

        internal CacheShardStore Shard { get; private set; }

        internal CacheReaderRegistry Pins { get; private set; }

        internal static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            var root = new CacheStoreRoot(new(new() { RootPath = fixture.path }), fixture.transient, fixture.resident);
            using var header = await root.OpenAsync(default);
            var identity = new CacheEntryIdentity("site", "items", "1", "key");
            fixture.Shard = new(root, root.ShardDirectory(identity), header.Value.StoreId, identity.CollectionIdentity, CacheStoreRoot.Shard(identity), 0,
                fixture.transient, fixture.resident, fixture.blocks);
            fixture.Pins = new(fixture.Shard.DirectoryPath, header.Value.StoreId, identity.CollectionIdentity, CacheStoreRoot.Shard(identity), 0,
                root.Profile, fixture.transient, fixture.resident, (_, bytes, token) => ReserveAsync(bytes, token), (_, _, _) => ValueTask.CompletedTask);
            return fixture;
        }

        internal ValueTask<CacheShardTransaction> AcquireAsync() => this.Shard.AcquireAsync(true, _ => ValueTask.FromResult(0L), ReserveAsync, default);

        internal ValueTask<CacheShardCheckpoint> CheckpointAsync(Func<long, CancellationToken, ValueTask<Result>> reserve) => CacheShardCheckpoint.PrepareAsync(
            this.Shard, this.Pins, new(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")), Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"),
            reserve, this.transient, this.resident, default);

        public ValueTask DisposeAsync()
        {
            this.blocks.Dispose();
            Directory.Delete(this.path, recursive: true);
            this.transient.Used.ShouldBe(0);
            this.resident.Used.ShouldBe(0);
            return ValueTask.CompletedTask;
        }
    }
}