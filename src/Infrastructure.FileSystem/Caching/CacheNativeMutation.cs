// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

// A native owner and durable root reservation precede all staging. The single root finalization guard
// remains outside source/payload transfer and cannot acquire a collection shard in the reverse order.
/// <summary>Owns the durable quota credit and native staging authority for one filesystem mutation through its actual termination.</summary>
/// <example><code>await using var mutation = await CacheNativeMutation.CreateAsync(ledger, shard, identity, nodeId, version, operationId, transient, resident, token);</code></example>
public sealed class CacheNativeMutation : IAsyncDisposable
{
    private readonly CacheRootQuotaLedger ledger;
    private readonly CacheShardStore shard;
    private readonly CacheSpaceCredit credit;
    private readonly FileSystemAuthority owner;
    private readonly ResourceCharge memory;
    private readonly SemaphoreSlim gate = new(1);
    private long reserved;
    private bool disposed;
    private bool attempted;

    private CacheNativeMutation(CacheRootQuotaLedger ledger, CacheShardStore shard, CacheSpaceCredit credit, FileSystemAuthority owner,
        CacheShardCommitIdentity commit, ResourceCharge memory)
    {
        this.ledger = ledger;
        this.shard = shard;
        this.credit = credit;
        this.owner = owner;
        this.Commit = commit;
        this.memory = memory;
        this.reserved = credit.ReservedBytes;
    }

    /// <summary>Gets the generated operation identity used by journal and reconciliation receipts.</summary>
    public string OperationId => this.credit.OperationId;

    /// <summary>Gets the durable quota credit identity retained until publication or proven cleanup.</summary>
    public string CreditId => this.credit.Id;

    /// <summary>Gets the predeclared output segment and manifest generation for this mutation.</summary>
    public CacheShardCommitIdentity Commit { get; }

    /// <summary>Gets the native staging-owner directory associated with the mutation&apos;s shard.</summary>
    public string OwnerDirectory => Path.Combine(this.shard.DirectoryPath, "stages");

    /// <summary>Reserves durable space before creating native staging authority and binding one predeclared commit identity.</summary>
    public static async ValueTask<CacheNativeMutation> CreateAsync(CacheRootQuotaLedger ledger, CacheShardStore shard,
        CacheEntryIdentity identity, string nodeId, string version, string operationId, ResourceBudget transient,
        ResourceBudget resident, CancellationToken cancellationToken, CacheSpaceCreditKind? kind = null)
    {
        if (!identity.IsValid || !Guid.TryParseExact(operationId, "N", out _) || string.IsNullOrEmpty(nodeId) || nodeId.Length > 128 ||
            (version is not null && !Guid.TryParseExact(version, "N", out _))) { throw new CacheTransferException(CacheErrorCode.Validation); }

        if (!ResourceCharge.TryReserve(128 * CacheDefaults.KiB, out var memory, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        FileSystemAuthority owner = null;
        try
        {
            var credit = new CacheSpaceCredit { Id = operationId, OperationId = operationId, Identity = identity, NodeId = nodeId,
                Owner = "stage-mutate-" + operationId, Version = version, Kind = kind ?? (version is null ? CacheSpaceCreditKind.Metadata : CacheSpaceCreditKind.Payload),
                PreserveMaintenanceHeadroom = version is not null && (kind is null or CacheSpaceCreditKind.Payload) };
            credit = await ledger.ReserveAsync(credit, Math.Min(2 * CacheDefaults.MiB, shard.Profile.Native.QuotaCreditBytes), cancellationToken).ConfigureAwait(false);
            // This visibility barrier must precede any subsequent root collection-epoch read.
            FileSystemPaths.RejectLinks(shard.DirectoryPath);
            FileSystemDurability.EnsureDirectory(shard.DirectoryPath);
            var owners = Path.Combine(shard.DirectoryPath, "stages");
            FileSystemPaths.RejectLinks(owners);
            FileSystemDurability.EnsureDirectory(owners);
            if (File.Exists(Path.Combine(owners, credit.Owner))) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            owner = await FileSystemAuthority.AcquireAsync(shard.Profile.AuthorityOptions, owners, credit.Owner, true, cancellationToken).ConfigureAwait(false);
            if (owner is null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            var commit = new CacheShardCommitIdentity(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
            await ledger.AttachCommitAsync(credit.Id, nodeId, commit, cancellationToken).ConfigureAwait(false);
            shard.Fault?.Invoke("credit-reserved");
            return new(ledger, shard, credit, owner, commit, memory);
        }
        catch
        {
            owner?.Dispose();
            memory.Dispose();
            // Admission may have durably succeeded. Only native reconciliation can return that credit.
            throw;
        }
    }

    /// <summary>Serializes growth of the mutation&apos;s durable quota reservation; disposal or lost native authority prevents further staging.</summary>
    public async ValueTask<Result> ReserveAsync(long requiredBytes, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            return await this.ReserveCoreAsync(requiredBytes, cancellationToken).ConfigureAwait(false);
        }
        finally { this.gate.Release(); }
    }

    /// <summary>Attempts publication once through the shard transaction and root quota guard, optionally coupling a tracked-operation checkpoint.</summary>
    public async ValueTask<CacheManifest> CommitAsync(CacheShardTransaction transaction, long expectedRootEpoch, CacheQuotaChange change,
        CancellationToken cancellationToken, long? shardEpoch = null, long existingStageBytes = 0, CacheNativeTrackedProgress trackedProgress = null)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (this.attempted || existingStageBytes < 0) { throw new CacheTransferException(CacheErrorCode.Contract); }

            this.attempted = true;
            this.owner.EnsureHeld(this.OwnerDirectory);
            return await transaction.CommitAsync(this.OperationId, (bytes, token) => this.ReserveCoreAsync(checked(bytes + existingStageBytes), token),
                (publish, token) => this.ledger.PublishAsync(this.credit.Id, this.credit.NodeId, expectedRootEpoch, change, publish, token,
                    transaction.NextSequence, transaction.Manifest.Generation, transaction.Manifest.Sequence, trackedProgress),
                cancellationToken, shardEpoch, this.Commit).ConfigureAwait(false);
        }
        finally { this.gate.Release(); }
    }

    private async ValueTask<Result> ReserveCoreAsync(long requiredBytes, CancellationToken cancellationToken)
    {
        this.owner.EnsureHeld(this.OwnerDirectory);
        if (requiredBytes < 0) { throw new CacheTransferException(CacheErrorCode.Validation); }

        while (requiredBytes > this.reserved)
        {
            var target = checked(this.reserved + Math.Min(requiredBytes - this.reserved, this.shard.Profile.Native.QuotaCreditBytes));
            var preferred = checked(this.reserved + Math.Min(this.shard.Profile.Native.QuotaCreditBytes, Math.Max(target - this.reserved, CacheDefaults.MiB)));
            var grown = await this.ledger.GrowAsync(this.credit.Id, this.credit.NodeId, target, cancellationToken, preferred).ConfigureAwait(false);
            this.reserved = grown.ReservedBytes;
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (this.disposed) { return; }

            this.disposed = true;
            try { await this.ledger.MarkNativeEndedAsync(this.credit.Id, this.credit.NodeId, CancellationToken.None).ConfigureAwait(false); }
            catch (Exception exception) when (exception is CacheTransferException or IOException or UnauthorizedAccessException or TimeoutException)
            {
                // An unacknowledged end remains charged. Native local-owner proof or explicit quiescent recovery can reconcile it.
            }
            finally { this.owner.Dispose(); this.memory.Dispose(); }

            // Files and reserved/prepared credits remain native-owned until exact publication/cleanup proof.
        }
        finally { this.gate.Release(); }
    }
}