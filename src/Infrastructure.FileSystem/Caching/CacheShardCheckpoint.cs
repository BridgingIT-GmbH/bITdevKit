// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Stages a bounded immutable index checkpoint outside shard authority and publishes only after comparing its entire input generation.</summary>
/// <example><code>await using var checkpoint = await CacheShardCheckpoint.PrepareAsync(store, readers, commit, operationId, journalId, reserve, transient, resident, token);</code></example>
public sealed class CacheShardCheckpoint : IAsyncDisposable
{
    private readonly CacheShardStore store;
    private readonly IAsyncDisposable pin;
    private readonly FileSystemAuthority owner;
    private readonly Func<long, CancellationToken, ValueTask<Result>> reserve;
    private readonly ResourceCharge memory;
    private readonly SemaphoreSlim gate = new(1);
    private bool disposed;
    private bool attempted;

    private CacheShardCheckpoint(CacheShardStore store, CacheManifest input, CacheManifest output, IAsyncDisposable pin,
        FileSystemAuthority owner, Func<long, CancellationToken, ValueTask<Result>> reserve, ResourceCharge memory)
    {
        this.store = store;
        this.Input = input;
        this.Output = output;
        this.pin = pin;
        this.owner = owner;
        this.reserve = reserve;
        this.memory = memory;
    }

    /// <summary>Gets the immutable manifest snapshot selected before compaction work began.</summary>
    public CacheManifest Input { get; }

    /// <summary>Gets the staged compacted manifest with its predeclared output segment, journal and publication generation.</summary>
    public CacheManifest Output { get; }

    /// <summary>Pins a checked input snapshot and stages its admitted compaction output without holding shard authority through the merge.</summary>
    public static async ValueTask<CacheShardCheckpoint> PrepareAsync(CacheShardStore store, CacheReaderRegistry readers,
        CacheShardCommitIdentity identity, string operationId, string journalId, Func<long, CancellationToken, ValueTask<Result>> reserve,
        ResourceBudget transient, ResourceBudget resident, CancellationToken cancellationToken, string expectedGeneration = null)
    {
        if (store.ControlLedger || identity is null || !Guid.TryParseExact(identity.SegmentId, "N", out _) ||
            !Guid.TryParseExact(identity.Generation, "N", out _) || !Guid.TryParseExact(operationId, "N", out _) || !Guid.TryParseExact(journalId, "N", out _))
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }

        IAsyncDisposable pin = null;
        FileSystemAuthority owner = null;
        if (!ResourceCharge.TryReserve(128 * CacheDefaults.KiB, out var memory, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        try
        {
            CacheManifest input;
            await using (var transaction = await store.AcquireAsync(false, null, null, cancellationToken).ConfigureAwait(false))
            {
                if (transaction is null || transaction.Manifest.Segments.Count == 0) { throw new CacheTransferException(CacheErrorCode.NotFound); }

                if (expectedGeneration is not null && transaction.Manifest.Generation != expectedGeneration) { throw new CacheTransferException(CacheErrorCode.Conflict); }

                input = transaction.Manifest with { Segments = transaction.Manifest.Segments.ToArray() };
                if ((await reserve(8192, cancellationToken).ConfigureAwait(false)).IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                owner = await FileSystemAuthority.AcquireAsync(store.Profile.AuthorityOptions, store.DirectoryPath,
                    "stage-compact-" + identity.SegmentId, true, cancellationToken).ConfigureAwait(false);
                if (owner is null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

                pin = await readers.RegisterAsync(transaction.Authority, "index", input.Segments.Select(s => s.Id).ToArray(), cancellationToken).ConfigureAwait(false);
            }

            // No shard/root authority spans the merge. Its immutable inputs remain protected by the native pin.
            var merged = await CacheIndexCompactor.CompactAsync(store.DirectoryPath, input.Segments, checked(input.Sequence + 1),
                async (bytes, token) => await reserve(checked(bytes + 8192), token).ConfigureAwait(false), store.Durable, transient, resident,
                cancellationToken, dropTombstones: true, outputId: identity.SegmentId, pruneSecondaryIndexes: true).ConfigureAwait(false);
            var stage = Path.Combine(store.DirectoryPath, journalId + ".journal.tmp");
            var target = Path.Combine(store.DirectoryPath, journalId + ".journal");
            FileSystemPaths.RejectLinks(stage);
            FileSystemPaths.RejectLinks(target);
            await using (var journal = FileSystemFileOperations.Open(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await journal.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (store.Durable) { FileSystemDurability.Flush(journal); }
            }

            FileSystemDurability.Move(stage, target, false, store.Durable);
            var output = input with { Generation = identity.Generation, Sequence = checked(input.Sequence + 1), CheckpointSequence = checked(input.Sequence + 1),
                Journal = journalId, JournalBytes = 0, Segments = [merged], OperationId = operationId,
                MetadataCharge = checked(merged.IndexBytes + merged.RecordsBytes + 8192) };
            return new(store, input, output, pin, owner, reserve, memory);
        }
        catch
        {
            // Root credit and its predeclared output identities survive until native cleanup proves non-publication.
            try { if (pin is not null) { await pin.DisposeAsync().ConfigureAwait(false); } }
            finally { owner?.Dispose(); memory.Dispose(); }

            throw;
        }
    }

    /// <summary>Publishes staged checkpoint output only if the exact input generation is still current; competing mutations prevent replacement.</summary>
    public async ValueTask<CacheManifest> PublishAsync(Func<Func<CancellationToken, ValueTask<long>>, CancellationToken, ValueTask<long>> publicationGuard,
        CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (this.attempted) { throw new CacheTransferException(CacheErrorCode.Contract); }

            this.attempted = true;
            this.owner.EnsureHeld(this.store.DirectoryPath);
            await using var transaction = await this.store.AcquireAsync(false, null, null, cancellationToken).ConfigureAwait(false);
            if (transaction is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            return await transaction.PublishCheckpointAsync(this.Input.Generation, this.Output, this.reserve, publicationGuard, cancellationToken).ConfigureAwait(false);
        }
        catch (CacheTransferException exception) when (exception.Code == CacheErrorCode.Indeterminate) { this.owner.RetainIfShared(); throw; }
        finally { this.gate.Release(); }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (this.disposed) { return; }

            this.disposed = true;
            try { await this.pin.DisposeAsync().ConfigureAwait(false); }
            finally { this.owner.Dispose(); this.memory.Dispose(); }
        }
        finally { this.gate.Release(); }
    }
}