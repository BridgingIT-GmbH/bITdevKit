// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using System.Security.Cryptography;
using System.Buffers.Binary;

/// <summary>Serializes durable root quota, collection boundaries, publication receipts and tracked-operation checkpoints without owning application payload transfers.</summary>
/// <example><code>var quota = await ledger.GetQuotaAsync(token); // Use the provider for application cache operations.</code></example>
public sealed class CacheRootQuotaLedger
{
    private static readonly CacheIndexKey QuotaKey = new(new byte[] { 1 });
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 32 };
    private readonly CacheStoreRoot root;
    private readonly CacheStoreHeader header;
    private readonly CacheShardStore store;
    private readonly ResourceBudget transient;
    private readonly ResourceBudget resident;
    private readonly TimeProvider time;
    private readonly long baselineBytes;
    private readonly CacheStoreHeader recoveryFence;

    /// <summary>Binds the control ledger to one inspected root generation; the optional recovery fence is reserved for explicitly quiescent recovery.</summary>
    public CacheRootQuotaLedger(CacheStoreRoot root, CacheStoreHeader header, long rootHeaderBytes, ResourceBudget transient,
        ResourceBudget resident, CacheIndexBlockCache blocks, TimeProvider time, CacheStoreHeader recoveryFence = null)
    {
        this.root = root;
        this.header = header;
        this.transient = transient;
        this.resident = resident;
        this.time = time;
        this.recoveryFence = recoveryFence;
        this.baselineBytes = rootHeaderBytes;
        this.store = new(root, root.Resolve("ledger"), header.StoreId, "root-ledger", 0, header.RecoveryGeneration, transient, resident, blocks, forceDurableMetadata: true);
    }

    /// <summary>Returns conservative quota attribution including control workspace; retained uncertain credits make the snapshot inexact.</summary>
    public ValueTask<CacheProviderQuota> GetQuotaAsync(CancellationToken cancellationToken) => this.ExecuteAsync(async (transaction, quota, token) =>
    {
        await ValueTask.CompletedTask;
        return (quota, quota.Snapshot(this.OwnedBytes(transaction)) with { IsExact = false });
    }, cancellationToken);

    /// <summary>Registers a logical collection&apos;s quotas or rejects changed limits for an existing durable collection.</summary>
    public ValueTask RegisterCollectionAsync(CacheProviderCollection definition, CancellationToken cancellationToken) =>
        this.ExecuteVoidAsync(async (transaction, quota, token) =>
        {
            var identity = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog");
            var key = CollectionKey(identity.CollectionIdentity);
            using var stored = await transaction.ReadAsync(key, token).ConfigureAwait(false);
            var previous = Decode<CacheCollectionQuotaState>(stored);
            if (previous is not null)
            {
                previous.Validate(identity.CollectionIdentity);
                if (previous.MaximumBytes != definition.Policies.Limits.MaxStorageBytes || previous.MaximumEntries != definition.Policies.Limits.MaxEntries)
                {
                    throw new CacheTransferException(CacheErrorCode.Compatibility);
                }
            }
            else
            {
                Set(transaction, key, new CacheCollectionQuotaState { Identity = identity.CollectionIdentity,
                    MaximumBytes = definition.Policies.Limits.MaxStorageBytes, MaximumEntries = definition.Policies.Limits.MaxEntries });
            }

            return quota;
        }, cancellationToken);

    /// <summary>Durably admits a new bounded space credit against both store and collection limits, or returns an exactly matching existing reserved credit.</summary>
    public ValueTask<CacheSpaceCredit> ReserveAsync(CacheSpaceCredit requested, long bytes, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (transaction, quota, token) =>
        {
            ValidateCredit(requested);
            if (bytes <= 0 || bytes > this.root.Profile.Native.QuotaCreditBytes) { throw new CacheTransferException(CacheErrorCode.Validation); }

            var key = CreditKey(requested.Id);
            using var stored = await transaction.ReadAsync(key, token).ConfigureAwait(false);
            var previous = Decode<CacheSpaceCredit>(stored);
            if (previous is not null)
            {
                if (previous.Identity != requested.Identity || previous.NodeId != requested.NodeId || previous.Owner != requested.Owner ||
                    previous.Version != requested.Version || previous.Kind != requested.Kind || previous.PreserveMaintenanceHeadroom != requested.PreserveMaintenanceHeadroom ||
                    previous.State != CacheSpaceCreditState.Reserved)
                {
                    throw new CacheTransferException(CacheErrorCode.Conflict);
                }

                return (quota, previous);
            }

            var collection = await this.CollectionAsync(transaction, requested.Identity.CollectionIdentity, token).ConfigureAwait(false);
            if (bytes > this.AdmissionAvailable(quota, collection, requested)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            var credit = requested with { ReservedBytes = bytes, CreatedTicks = this.time.GetUtcNow().UtcTicks, State = CacheSpaceCreditState.Reserved,
                CollectionId = CacheStoreRoot.Digest(requested.Identity.CollectionIdentity), PreparedChange = null, PublicationGeneration = null };
            var next = quota.Reserve(bytes);
            Set(transaction, CollectionKey(collection.Identity), collection.Reserve(bytes));
            Set(transaction, key, credit);
            return (next, credit);
        }, cancellationToken);

    /// <summary>Grows a caller-owned reserved credit by at most one configured credit increment, preferring extra headroom only when both quotas permit it.</summary>
    public ValueTask<CacheSpaceCredit> GrowAsync(string id, string nodeId, long requiredBytes, CancellationToken cancellationToken, long preferredBytes = 0) =>
        this.ExecuteAsync(async (transaction, quota, token) =>
        {
            var credit = await this.CreditAsync(transaction, id, token).ConfigureAwait(false);
            if (credit.State != CacheSpaceCreditState.Reserved || credit.NodeId != nodeId || requiredBytes < 0 || preferredBytes < 0 ||
                (preferredBytes != 0 && preferredBytes < requiredBytes)) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            var delta = requiredBytes - credit.ReservedBytes;
            if (delta <= 0) { return (quota, credit); }

            if (delta > this.root.Profile.Native.QuotaCreditBytes) { throw new CacheTransferException(CacheErrorCode.Validation); }

            var collection = await this.CollectionAsync(transaction, credit.Identity.CollectionIdentity, token).ConfigureAwait(false);
            var available = this.AdmissionAvailable(quota, collection, credit);
            if (delta > available) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            var wanted = preferredBytes == 0 ? delta : Math.Min(this.root.Profile.Native.QuotaCreditBytes, preferredBytes - credit.ReservedBytes);
            delta = Math.Min(wanted, available);
            var next = quota.Reserve(delta);
            credit = credit with { ReservedBytes = checked(credit.ReservedBytes + delta) };
            Set(transaction, CollectionKey(collection.Identity), collection.Reserve(delta));
            Set(transaction, CreditKey(id), credit);
            return (next, credit);
        }, cancellationToken);

    private long AdmissionAvailable(CacheRootQuotaState quota, CacheCollectionQuotaState collection, CacheSpaceCredit credit)
    {
        if (!credit.PreserveMaintenanceHeadroom) { return Math.Min(quota.Available, collection.Available); }

        // Index copy-on-write needs room alongside all of its still-pinned immutable inputs.
        // Existing metadata attribution is a conservative bound on a complete compacted copy.
        // Keep two additional MiB for one bounded metadata mutation and bootstrap/reader records.
        long Headroom(long maximum, long metadata) => Math.Max((long)(maximum * this.root.Profile.Native.MetadataReserveRatio),
            metadata > maximum - 2 * CacheDefaults.MiB ? maximum : metadata + 2 * CacheDefaults.MiB);
        return Math.Max(0, Math.Min(quota.Available - Headroom(quota.MaximumBytes, quota.MetadataBytes),
            collection.Available - Headroom(collection.MaximumBytes, collection.MetadataBytes)));
    }

    /// <summary>Reads and validates one exact durable space credit by generated ID.</summary>
    public ValueTask<CacheSpaceCredit> GetCreditAsync(string id, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (transaction, quota, token) => (quota, await this.CreditAsync(transaction, id, token).ConfigureAwait(false)), cancellationToken);

    /// <summary>Reads the durable logical collection epoch used by source publication fences.</summary>
    public ValueTask<long> GetEpochAsync(string collectionIdentity, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (transaction, quota, token) => (quota, (await this.CollectionAsync(transaction, collectionIdentity, token).ConfigureAwait(false)).Epoch), cancellationToken);

    /// <summary>Atomically advances the durable collection epoch so previously captured source rights can no longer publish.</summary>
    public ValueTask<long> AdvanceEpochAsync(string collectionIdentity, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (transaction, quota, token) =>
        {
            var collection = await this.CollectionAsync(transaction, collectionIdentity, token).ConfigureAwait(false);
            collection = collection with { Epoch = checked(collection.Epoch + 1) };
            Set(transaction, CollectionKey(collectionIdentity), collection);
            return (quota, collection.Epoch);
        }, cancellationToken);

    private async ValueTask<T> ExecuteAsync<T>(Func<CacheShardTransaction, CacheRootQuotaState, CancellationToken, ValueTask<(CacheRootQuotaState Quota, T Value)>> action,
        CancellationToken cancellationToken)
    {
        // The persisted root fence also excludes a previously constructed provider after offline recovery/reset.
        using (var current = await CacheCheckedMetadata.ReadAsync<CacheStoreHeader>(this.root.Resolve("cache-root.json"), this.transient, this.resident,
            cancellationToken).ConfigureAwait(false))
        {
            current.Value.Validate(this.root.Profile);
            var expected = this.recoveryFence ?? this.header;
            if (current.Value.StoreId != expected.StoreId || current.Value.RecoveryGeneration != expected.RecoveryGeneration ||
                current.Value.RecoveryInProgress != (this.recoveryFence is not null)) { throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

            if ((!File.Exists(Path.Combine(this.store.DirectoryPath, "header.json")) || !File.Exists(Path.Combine(this.store.DirectoryPath, "manifest.json"))) &&
                (current.Value.LedgerInitialized || Directory.Exists(this.root.Resolve("collections"))))
            {
                throw new CacheTransferException(CacheErrorCode.Corruption);
            }
        }

        if (!ResourceCharge.TryReserve(2 * CacheDefaults.MiB, out var memory, this.transient, this.resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        using (memory)
        await using (var transaction = await this.store.AcquireAsync(true, _ => ValueTask.FromResult(0L), this.ReserveBootstrapAsync, cancellationToken).ConfigureAwait(false))
        {
            // Native authority proves no writer still uses these predeclared, never-visible candidates.
            // A missing committed manifest has already failed acquisition and is never reconstructed here.
            foreach (var name in new[] { "manifest.json.write.tmp", "intent.json.write.tmp" })
            {
                var abandoned = Path.Combine(this.store.DirectoryPath, name);
                FileSystemPaths.RejectLinks(abandoned);
                File.Delete(abandoned);
            }

            await this.ResolveIntentAsync(transaction, cancellationToken).ConfigureAwait(false);
            using var stored = await transaction.ReadAsync(QuotaKey, cancellationToken).ConfigureAwait(false);
            var quota = Decode<CacheRootQuotaState>(stored);
            if (quota is null)
            {
                if (transaction.Manifest.Sequence != 0) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                quota = new() { StoreId = this.header.StoreId, MaximumBytes = this.header.MaximumBytes, MaximumEntries = this.header.MaximumEntries,
                    WorkspaceBytes = this.InitialWorkspace() };
                Set(transaction, QuotaKey, quota);
            }

            quota.Validate(this.header.StoreId);
            if (transaction.MutationCount == 0) { await this.CompactAsync(transaction, quota, cancellationToken).ConfigureAwait(false); }

            var outcome = await action(transaction, quota, cancellationToken).ConfigureAwait(false);
            if (transaction.MutationCount != 0 || outcome.Quota != quota)
            {
                await this.PersistAsync(transaction, outcome.Quota, cancellationToken).ConfigureAwait(false);
            }

            return outcome.Value;
        }
    }

    private async ValueTask ExecuteVoidAsync(Func<CacheShardTransaction, CacheRootQuotaState, CancellationToken, ValueTask<CacheRootQuotaState>> action,
        CancellationToken cancellationToken) => await this.ExecuteAsync(async (transaction, quota, token) =>
            (await action(transaction, quota, token).ConfigureAwait(false), true), cancellationToken).ConfigureAwait(false);

    private async ValueTask<CacheRootQuotaState> PersistAsync(CacheShardTransaction transaction, CacheRootQuotaState quota, CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var commit = new CacheShardCommitIdentity(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        Set(transaction, QuotaKey, quota);
        var owned = this.OwnedBytes(transaction);
        var required = checked(2 * owned + transaction.EstimateCommitGrowth(commit, operationId));
        if (required > quota.WorkspaceBytes)
        {
            var growth = required - quota.WorkspaceBytes;
            if (growth > quota.Available) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            quota = quota with { WorkspaceBytes = required + Math.Min(quota.Available - growth, 65536) };
            Set(transaction, QuotaKey, quota);
            required = checked(2 * owned + transaction.EstimateCommitGrowth(commit, operationId));
            if (required > quota.WorkspaceBytes) { throw new CacheTransferException(CacheErrorCode.Capacity); }
        }

        quota.Validate(this.header.StoreId);
        var intent = new CacheLedgerIntent(this.header.StoreId, operationId, transaction.Manifest.Generation, commit, transaction.Manifest.Journal,
            transaction.Manifest.JournalBytes, quota.WorkspaceBytes, owned);
        await CacheCheckedMetadata.WriteAsync(this.store.DirectoryPath, "intent.json", intent, transaction.Authority, true, this.transient, this.resident, cancellationToken).ConfigureAwait(false);
        try
        {
            await transaction.CommitAsync(operationId, (bytes, _) => ValueTask.FromResult(bytes <= quota.WorkspaceBytes - owned ? Result.Success() :
                    Result.Failure(new CacheError(CacheErrorCode.Capacity, "Native ledger workspace admission was rejected."))),
                null, cancellationToken, identity: commit).ConfigureAwait(false);
            this.DeleteIntent();
            return quota;
        }
        catch
        {
            transaction.Authority.RetainIfShared();
            throw;
        }
    }

    private async ValueTask ResolveIntentAsync(CacheShardTransaction transaction, CancellationToken cancellationToken)
    {
        var path = Path.Combine(this.store.DirectoryPath, "intent.json");
        if (!File.Exists(path)) { return; }

        using var retained = await CacheCheckedMetadata.ReadAsync<CacheLedgerIntent>(path, this.transient, this.resident, cancellationToken).ConfigureAwait(false);
        var intent = retained.Value;
        if (intent.StoreId != this.header.StoreId || !Guid.TryParseExact(intent.OperationId, "N", out _) || intent.Commit is null ||
            !Guid.TryParseExact(intent.Commit.SegmentId, "N", out _) || !Guid.TryParseExact(intent.Commit.Generation, "N", out _) ||
            !Guid.TryParseExact(intent.BaseGeneration, "N", out _) || !Guid.TryParseExact(intent.Journal, "N", out _) ||
            intent.CommittedJournalBytes is < 0 or > 32 * CacheDefaults.MiB || intent.PreviousOwnedBytes < 0 ||
            intent.WorkspaceBytes < intent.PreviousOwnedBytes || intent.WorkspaceBytes <= 0 || intent.WorkspaceBytes > this.header.MaximumBytes ||
            (intent.CheckpointJournal is not null && (!Guid.TryParseExact(intent.CheckpointJournal, "N", out _) || intent.CheckpointJournal == intent.Journal)) ||
            (intent.RetiredSegments is not null && (intent.RetiredSegments.Count > CacheManifest.MaximumSegments ||
                intent.RetiredSegments.Any(s => !Guid.TryParseExact(s, "N", out _)) || intent.RetiredSegments.Distinct(StringComparer.Ordinal).Count() != intent.RetiredSegments.Count)))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        if (transaction.Manifest.Generation == intent.Commit.Generation && transaction.Manifest.OperationId == intent.OperationId)
        {
            if (intent.CheckpointJournal is not null) { this.DeleteRetiredControlFiles(intent, transaction.Manifest); }

            this.DeleteIntent();
            return;
        }

        if (transaction.Manifest.Generation != intent.BaseGeneration || transaction.Manifest.Journal != intent.Journal ||
            transaction.Manifest.JournalBytes != intent.CommittedJournalBytes || transaction.Manifest.Segments.Any(s => s.Id == intent.Commit.SegmentId))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        foreach (var suffix in new[] { ".idx", ".records", ".idx.tmp", ".records.tmp" })
        {
            var candidate = Path.Combine(this.store.DirectoryPath, intent.Commit.SegmentId + suffix);
            FileSystemPaths.RejectLinks(candidate);
            File.Delete(candidate);
        }

        if (intent.CheckpointJournal is not null)
        {
            if (!Guid.TryParseExact(intent.CheckpointJournal, "N", out _)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            foreach (var suffix in new[] { ".journal", ".journal.tmp" })
            {
                var candidate = Path.Combine(this.store.DirectoryPath, intent.CheckpointJournal + suffix);
                FileSystemPaths.RejectLinks(candidate);
                File.Delete(candidate);
            }
        }

        var journalPath = Path.Combine(this.store.DirectoryPath, intent.Journal + ".journal");
        FileSystemPaths.RejectLinks(journalPath);
        await using (var journal = FileSystemFileOperations.Open(journalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete))
        {
            if (journal.Length < intent.CommittedJournalBytes) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            journal.SetLength(intent.CommittedJournalBytes);
            FileSystemDurability.Flush(journal);
        }

        this.DeleteIntent();
    }

    private void DeleteIntent()
    {
        File.Delete(Path.Combine(this.store.DirectoryPath, "intent.json"));
        if (OperatingSystem.IsLinux()) { FileSystemDurability.SyncDirectory(this.store.DirectoryPath); }
    }

    private long OwnedBytes(CacheShardTransaction transaction) => checked(this.baselineBytes + transaction.ReferencedBytes +
        new FileInfo(Path.Combine(this.store.DirectoryPath, "header.json")).Length + (this.root.Profile.Shared ? 32 : 0));

    private long InitialWorkspace()
    {
        var bytes = Math.Max(2 * CacheDefaults.MiB, (long)(this.header.MaximumBytes * this.root.Profile.Native.MetadataReserveRatio));
        if (bytes >= this.header.MaximumBytes) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        return bytes;
    }

    private ValueTask<Result> ReserveBootstrapAsync(long bytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(bytes <= this.InitialWorkspace() ? Result.Success() : Result.Failure(new CacheError(CacheErrorCode.Capacity, "Ledger bootstrap exceeds shared workspace.")));
    }

    private async ValueTask<CacheCollectionQuotaState> CollectionAsync(CacheShardTransaction transaction, string identity, CancellationToken token)
    {
        using var stored = await transaction.ReadAsync(CollectionKey(identity), token).ConfigureAwait(false);
        var collection = Decode<CacheCollectionQuotaState>(stored) ?? throw new CacheTransferException(CacheErrorCode.Contract);
        collection.Validate(identity);
        return collection;
    }

    private async ValueTask<CacheSpaceCredit> CreditAsync(CacheShardTransaction transaction, string id, CancellationToken token)
    {
        using var stored = await transaction.ReadAsync(CreditKey(id), token).ConfigureAwait(false);
        var credit = Decode<CacheSpaceCredit>(stored) ?? throw new CacheTransferException(CacheErrorCode.NotFound);
        ValidateCredit(credit);
        if (credit.Id != id) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        return credit;
    }

    private static T Decode<T>(CacheShardValue value) where T : class
    {
        if (value is null || value.Deleted) { return null; }

        try { return JsonSerializer.Deserialize<T>(value.Bytes.Span, Json) ?? throw new CacheTransferException(CacheErrorCode.Corruption); }
        catch (JsonException) { throw new CacheTransferException(CacheErrorCode.Corruption); }
    }

    private static void Set<T>(CacheShardTransaction transaction, CacheIndexKey key, T value)
    {
        using var buffer = new CacheMetadataBuffer();
        JsonSerializer.Serialize(buffer, value, Json);
        transaction.Set(key, buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
    }

    private static CacheIndexKey CollectionKey(string identity) => new(new byte[] { 2 }.Concat(Convert.FromHexString(CacheStoreRoot.Digest(identity))).ToArray());

    private static CacheIndexKey CreditKey(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) { throw new CacheTransferException(CacheErrorCode.Validation); }

        return new(new byte[] { 3 }.Concat(Convert.FromHexString(id)).ToArray());
    }

    private static void ValidateCredit(CacheSpaceCredit credit)
    {
        if (credit is null || !Guid.TryParseExact(credit.Id, "N", out _) || !Guid.TryParseExact(credit.OperationId, "N", out _) ||
            !credit.Identity.IsValid || string.IsNullOrEmpty(credit.NodeId) || credit.NodeId.Length > 128 || string.IsNullOrEmpty(credit.Owner) || credit.Owner.Length > 128 ||
            credit.ReservedBytes < 0 || !Enum.IsDefined(credit.State) || !Enum.IsDefined(credit.Kind) || (credit.Version is not null && !Guid.TryParseExact(credit.Version, "N", out _)))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    private static readonly CacheIndexKey CursorKey = new(new byte[] { 5 });

    /// <summary>Registers a bounded canonical value contract and its schema index, rejecting incompatible existing contract identities or schemas.</summary>
    public ValueTask<CacheNativeContract> RegisterContractAsync(CacheProviderCollection definition, CancellationToken cancellationToken)
    {
        var requested = CacheNativeContract.From(definition);
        return this.ExecuteAsync(async (transaction, quota, token) =>
        {
            _ = await this.CollectionAsync(transaction, requested.Identity.CollectionIdentity, token).ConfigureAwait(false);
            var key = ContractKey(requested.Identity);
            using var stored = await transaction.ReadAsync(key, token).ConfigureAwait(false);
            var previous = Decode<CacheNativeContract>(stored);
            if (previous is not null)
            {
                previous.Validate(requested.Identity);
                if (previous.ContractId != requested.ContractId || JsonSerializer.Serialize(previous.Dimensions) != JsonSerializer.Serialize(requested.Dimensions))
                {
                    throw new CacheTransferException(CacheErrorCode.Compatibility);
                }

                using var reference = await transaction.ReadAsync(ContractReferenceKey(requested.Identity), token).ConfigureAwait(false);
                if (reference is null || reference.Deleted) { Set(transaction, ContractReferenceKey(requested.Identity), new CacheNativeContractReference(1, requested.Identity)); }

                return (quota, previous);
            }

            if (quota.CatalogContracts >= 1024) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            Set(transaction, key, requested);
            Set(transaction, ContractReferenceKey(requested.Identity), new CacheNativeContractReference(1, requested.Identity));
            return (quota with { CatalogContracts = quota.CatalogContracts + 1 }, requested);
        }, cancellationToken);
    }

    /// <summary>Reads and validates the exact persisted value contract, reporting NotFound when absent.</summary>
    public ValueTask<CacheNativeContract> GetContractAsync(CacheEntryIdentity identity, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (transaction, quota, token) =>
        {
            using var stored = await transaction.ReadAsync(ContractKey(identity), token).ConfigureAwait(false);
            var contract = Decode<CacheNativeContract>(stored) ?? throw new CacheTransferException(CacheErrorCode.NotFound);
            contract.Validate(identity);
            return (quota, contract);
        }, cancellationToken);

    /// <summary>Reads or durably creates the private 32-byte continuation-encryption key; callers must never log or expose this key.</summary>
    public ValueTask<byte[]> GetCursorKeyAsync(CancellationToken cancellationToken) => this.ExecuteAsync(async (transaction, quota, token) =>
    {
        using var stored = await transaction.ReadAsync(CursorKey, token).ConfigureAwait(false);
        if (stored is not null && !stored.Deleted)
        {
            if (stored.Bytes.Length != 32) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            return (quota, stored.Bytes.ToArray());
        }

        var key = RandomNumberGenerator.GetBytes(32);
        transaction.Set(CursorKey, key);
        return (quota, key);
    }, cancellationToken);

    private static CacheIndexKey ContractKey(CacheEntryIdentity identity) => new(new byte[] { 4 }.Concat(Convert.FromHexString(
        new CacheEntryIdentity(identity.Namespace, identity.Collection, identity.ContractVersion, "catalog").Digest)).ToArray());

    private static CacheIndexKey ContractReferenceKey(CacheEntryIdentity identity) => new(new byte[] { 9 }
        .Concat(Convert.FromHexString(CacheStoreRoot.Digest(identity.CollectionIdentity)))
        .Concat(Convert.FromHexString(new CacheEntryIdentity(identity.Namespace, identity.Collection, identity.ContractVersion, "catalog").Digest)).ToArray());

    private async ValueTask CompactAsync(CacheShardTransaction transaction, CacheRootQuotaState quota, CancellationToken cancellationToken)
    {
        if (transaction.Manifest.Segments.Count < 32 && transaction.Manifest.JournalBytes < this.root.Profile.Native.CheckpointBytes) { return; }

        var commit = new CacheShardCommitIdentity(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        var operationId = Guid.NewGuid().ToString("N");
        var journal = Guid.NewGuid().ToString("N");
        var original = transaction.Manifest;
        var owned = this.OwnedBytes(transaction);
        var intent = new CacheLedgerIntent(this.header.StoreId, operationId, original.Generation, commit, original.Journal, original.JournalBytes,
            quota.WorkspaceBytes, owned, journal, original.Segments.Select(s => s.Id).ToArray());
        await CacheCheckedMetadata.WriteAsync(this.store.DirectoryPath, "intent.json", intent, transaction.Authority, true, this.transient, this.resident, cancellationToken).ConfigureAwait(false);
        try
        {
            var merged = await CacheIndexCompactor.CompactAsync(this.store.DirectoryPath, original.Segments, transaction.NextSequence,
                (bytes, _) => ValueTask.FromResult(bytes + owned + 8192 <= quota.WorkspaceBytes ? Result.Success() :
                    Result.Failure(new CacheError(CacheErrorCode.Capacity, "Ledger checkpoint workspace admission was rejected."))),
                true, this.transient, this.resident, cancellationToken, dropTombstones: true, outputId: commit.SegmentId).ConfigureAwait(false);
            await transaction.CheckpointControlAsync(merged, commit, journal, operationId, cancellationToken).ConfigureAwait(false);
            await transaction.RefreshAfterCommitAsync(cancellationToken).ConfigureAwait(false);
            this.DeleteRetiredControlFiles(intent, transaction.Manifest);
            this.DeleteIntent();
        }
        catch
        {
            transaction.Authority.RetainIfShared();
            throw;
        }
    }

    private void DeleteRetiredControlFiles(CacheLedgerIntent intent, CacheManifest current)
    {
        foreach (var id in intent.RetiredSegments ?? [])
        {
            if (!Guid.TryParseExact(id, "N", out _) || current.Segments.Any(s => s.Id == id)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            foreach (var suffix in new[] { ".idx", ".records" })
            {
                var path = Path.Combine(this.store.DirectoryPath, id + suffix);
                FileSystemPaths.RejectLinks(path);
                File.Delete(path);
            }
        }

        if (intent.CheckpointJournal is not null)
        {
            if (current.Journal == intent.Journal) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            var path = Path.Combine(this.store.DirectoryPath, intent.Journal + ".journal");
            FileSystemPaths.RejectLinks(path);
            File.Delete(path);
        }
    }

    /// <summary>Binds a reserved compaction credit to an immutable input plan before any compaction output is written.</summary>
    public ValueTask AttachCompactionAsync(string id, string nodeId, CacheNativeCompactionPlan plan, CancellationToken cancellationToken) =>
        this.ExecuteVoidAsync(async (transaction, quota, token) =>
        {
            var credit = await this.CreditAsync(transaction, id, token).ConfigureAwait(false);
            if (credit.NodeId != nodeId || credit.Kind != CacheSpaceCreditKind.Compaction || credit.State != CacheSpaceCreditState.Reserved ||
                credit.Compaction is not null || plan.InputBytes < 0 || plan.InputSequence < 0 || plan.Segments is null || plan.Segments.Count is <= 0 or > 64 ||
                plan.Segments.Any(s => !Guid.TryParseExact(s, "N", out _)) || !Guid.TryParseExact(plan.InputGeneration, "N", out _) ||
                !Guid.TryParseExact(plan.Journal, "N", out _) || plan.OutputJournal != id) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            Set(transaction, CreditKey(id), credit with { Compaction = plan });
            return quota;
        }, cancellationToken);

    /// <summary>Returns the exact old metadata attribution once after the caller has physically deleted the committed compaction inputs.</summary>
    public ValueTask ReclaimCompactionAsync(string id, CancellationToken cancellationToken) => this.ExecuteVoidAsync(async (transaction, quota, token) =>
    {
        var credit = await this.CreditAsync(transaction, id, token).ConfigureAwait(false);
        if (credit.Kind != CacheSpaceCreditKind.Compaction || credit.State != CacheSpaceCreditState.Committed || credit.Compaction is null)
        {
            throw new CacheTransferException(CacheErrorCode.Conflict);
        }

        if (credit.CompactionReclaimed) { return quota; }

        var change = new CacheQuotaChange(MetadataBytes: -credit.Compaction.InputBytes);
        var collection = await this.CollectionAsync(transaction, credit.Identity.CollectionIdentity, token).ConfigureAwait(false);
        Set(transaction, CollectionKey(collection.Identity), collection.Apply(change, 0));
        Set(transaction, CreditKey(id), credit with { CompactionReclaimed = true });
        return quota.Apply(change, 0);
    }, cancellationToken);

    /// <summary>Durably marks a matching node&apos;s native work as actually terminated; a timeout alone never authorizes this transition.</summary>
    public ValueTask MarkNativeEndedAsync(string id, string nodeId, CancellationToken cancellationToken) =>
        this.ExecuteVoidAsync(async (transaction, quota, token) =>
        {
            var credit = await this.CreditAsync(transaction, id, token).ConfigureAwait(false);
            if (credit.NodeId != nodeId) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            if (!credit.NativeEnded) { Set(transaction, CreditKey(id), credit with { NativeEnded = true }); }

            return quota;
        }, cancellationToken);

    /// <summary>Returns a charged bounded page of checked credits after the exclusive continuation key; dispose the page before retaining no further records.</summary>
    public ValueTask<CacheNativeCreditPage> GetCreditsAsync(CacheIndexKey after, int limit, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (transaction, quota, token) =>
        {
            var range = CacheMetadataIndex.PrefixRange(new byte[] { 3 });
            var lower = after?.After() ?? range.Lower;
            var rows = new List<CacheSpaceCredit>();
            var charges = new List<ResourceCharge>();
            var last = after;
            var examined = 0;
            var ended = true;
            try
            {
                await foreach (var row in transaction.ScanAsync(lower, limit, token, range.Upper).ConfigureAwait(false))
                {
                    examined++;
                    last = row.Key;
                    if (!row.Deleted)
                    {
                        if (row.Bytes.Length > 131072 || !ResourceCharge.TryReserve(33L * row.Bytes.Length + 65536, out var memory, this.transient, this.resident))
                        {
                            throw new CacheTransferException(CacheErrorCode.Capacity);
                        }

                        charges.Add(memory);
                        var credit = JsonSerializer.Deserialize<CacheSpaceCredit>(row.Bytes.Span, Json) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                        ValidateCredit(credit);
                        if (!CreditKey(credit.Id).Equals(row.Key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                        rows.Add(credit);
                    }

                    if (examined >= limit) { ended = false; }
                }

                return (quota, new CacheNativeCreditPage(rows.AsReadOnly(), ended ? null : last, examined, charges));
            }
            catch { foreach (var charge in charges) { charge.Dispose(); } throw; }
        }, cancellationToken);

    // Metadata charges remain attributed to their physical shard generations until checkpoint reclamation.
    /// <summary>Tombstones only released credits or committed metadata credits whose native work and any required compaction reclamation have ended.</summary>
    public ValueTask ForgetMetadataCreditAsync(string id, CancellationToken cancellationToken) => this.ExecuteVoidAsync(async (transaction, quota, token) =>
    {
        using var stored = await transaction.ReadAsync(CreditKey(id), token).ConfigureAwait(false);
        var credit = Decode<CacheSpaceCredit>(stored);
        if (credit is null) { return quota; }

        ValidateCredit(credit);
        if ((credit.Kind == CacheSpaceCreditKind.Metadata && credit.State == CacheSpaceCreditState.Committed && credit.NativeEnded) ||
            (credit.Kind == CacheSpaceCreditKind.Compaction && credit.State == CacheSpaceCreditState.Committed && credit.NativeEnded && credit.CompactionReclaimed) ||
            credit.State == CacheSpaceCreditState.Released)
        {
            transaction.Set(CreditKey(id), [], deleted: true);
        }

        return quota;
    }, cancellationToken);

    /// <summary>Reaps a bounded batch of expired terminal tracked operations without evicting active work.</summary>
    public ValueTask MaintainOperationsAsync(int maximumRecords, TimeSpan retention, CancellationToken cancellationToken) =>
        this.ExecuteVoidAsync((transaction, quota, token) => this.ReapOperationsAsync(transaction, quota, maximumRecords, retention, false, token), cancellationToken);

    /// <summary>Forgets an exact committed payload credit only after its caller has proven that no current, retired, receipt or physical payload ownership remains.</summary>
    /// <param name="id">The generated durable payload credit identity.</param>
    /// <param name="identity">The full identity checked under its shard authority.</param>
    /// <param name="version">The immutable payload version whose physical ownership has ended.</param>
    /// <param name="cancellationToken">Cancels the bounded control-ledger update.</param>
    /// <remarks>This removes control bookkeeping only. Physical byte attribution must already have been returned by the conditional retirement transaction.</remarks>
    /// <example><code>await ledger.ForgetPayloadCreditAsync(credit.Id, credit.Identity, credit.Version, token);</code></example>
    public ValueTask ForgetPayloadCreditAsync(string id, CacheEntryIdentity identity, string version, CancellationToken cancellationToken) =>
        this.ExecuteVoidAsync(async (transaction, quota, token) =>
        {
            using var stored = await transaction.ReadAsync(CreditKey(id), token).ConfigureAwait(false);
            var credit = Decode<CacheSpaceCredit>(stored);
            if (credit is null) { return quota; }

            ValidateCredit(credit);
            if (credit.Kind != CacheSpaceCreditKind.Payload || credit.State != CacheSpaceCreditState.Committed || !credit.NativeEnded ||
                credit.Identity != identity || credit.Version != version) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            transaction.Set(CreditKey(id), [], deleted: true);
            return quota;
        }, cancellationToken);

    private async ValueTask<CacheRootQuotaState> ReapOperationsAsync(CacheShardTransaction transaction, CacheRootQuotaState quota, int maximumRecords,
        TimeSpan retention, bool admission, CancellationToken token)
    {
        var range = CacheMetadataIndex.PrefixRange(new byte[] { 13 });
        var selected = new List<string>();
        var cutoff = this.time.GetUtcNow() - retention;
        await foreach (var candidate in transaction.ScanAsync(range.Lower, 128, token, range.Upper).ConfigureAwait(false))
        {
            if (candidate.Deleted) { continue; }

            if (candidate.Bytes.Length != 16) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            var expired = BinaryPrimitives.ReadInt64BigEndian(candidate.Key.Bytes[1..9]) <= cutoff.UtcTicks;
            if (!expired && (!admission || quota.OperationRecords - selected.Count < Math.Min(1000, maximumRecords))) { break; }

            selected.Add(Convert.ToHexStringLower(candidate.Bytes.Span));
            if (selected.Count == 8) { break; }
        }

        var next = quota;
        foreach (var id in selected)
        {
            using var stored = await transaction.ReadAsync(OperationKey(id), token).ConfigureAwait(false);
            if (stored is null || stored.Deleted) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            if (stored.Bytes.Length > 131072 || !ResourceCharge.TryReserve(33L * stored.Bytes.Length + 65536, out var memory, this.transient, this.resident))
            {
                throw new CacheTransferException(CacheErrorCode.Capacity);
            }

            using (memory)
            {
                var record = Decode<CacheNativeOperationRecord>(stored) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                record.Validate(id);
                if (!TerminalOperation(record.Value.State) || record.PendingCreditId is not null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                var identity = new CacheEntryIdentity(record.Value.Request.Namespace, record.Value.Request.Collection, "operation", "scope");
                var collection = await this.CollectionAsync(transaction, identity.CollectionIdentity, token).ConfigureAwait(false);
                Set(transaction, CollectionKey(collection.Identity), collection.Apply(new(MetadataBytes: -OperationCreditBytes), 0));
                transaction.Set(OperationKey(id), [], deleted: true);
                transaction.Set(OperationTimeKey(record.Value), [], deleted: true);
                transaction.Set(OperationStateTimeKey(record.Value), [], deleted: true);
                next = next with { OperationRecords = checked(next.OperationRecords - 1) };
            }
        }

        return next;
    }

    private static void SetOperationRecord(CacheShardTransaction transaction, CacheNativeOperationRecord previous, CacheNativeOperationRecord value)
    {
        if (previous is not null)
        {
            transaction.Set(OperationTimeKey(previous.Value), [], deleted: true);
            transaction.Set(OperationStateTimeKey(previous.Value), [], deleted: true);
        }

        Set(transaction, OperationKey(value.Value.Id), value);
        var pointer = Convert.FromHexString(value.Value.Id);
        transaction.Set(OperationTimeKey(value.Value), pointer);
        transaction.Set(OperationStateTimeKey(value.Value), pointer);
    }

    private static CacheIndexKey OperationStateTimeKey(CacheProviderOperation operation)
    {
        Span<byte> bytes = stackalloc byte[25];
        bytes[0] = TerminalOperation(operation.State) ? (byte)13 : (byte)12;
        BinaryPrimitives.WriteInt64BigEndian(bytes[1..], operation.UpdatedAt.UtcTicks);
        Convert.FromHexString(operation.Id).CopyTo(bytes[9..]);
        return new(bytes);
    }

    private static bool TerminalOperation(CacheOperationState state) => state is CacheOperationState.Completed or CacheOperationState.Cancelled or CacheOperationState.Failed;

    private const long OperationCreditBytes = 132 * CacheDefaults.KiB;

    /// <summary>Creates or compare-and-swaps a tracked operation under bounded history retention, preserving execution claims during remote cancellation requests.</summary>
    public ValueTask<CacheProviderOperation> SaveOperationAsync(CacheProviderOperation requested, string fingerprint, string nodeId, string expectedRevision,
        int maximumRecords, TimeSpan retention, CancellationToken cancellationToken) => this.ExecuteAsync(async (transaction, quota, token) =>
    {
        ValidateOperationInput(requested);
        var key = OperationKey(requested.Id);
        using var stored = await transaction.ReadAsync(key, token).ConfigureAwait(false);
        var previous = Decode<CacheNativeOperationRecord>(stored);
        previous?.Validate(requested.Id);
        var now = this.time.GetUtcNow();
        var cancellation = previous?.Value.State == CacheOperationState.Running && requested.State == CacheOperationState.Running && requested.CancellationRequested;
        if (previous is not null && ((!cancellation && previous.PendingCreditId is not null) || expectedRevision != previous.Value.Revision ||
            previous.SelectionHash != fingerprint || previous.Value.Cutoff != requested.Cutoff || previous.Value.Request.Clear != requested.Request.Clear ||
            !StringComparer.OrdinalIgnoreCase.Equals(previous.Value.Request.Namespace, requested.Request.Namespace) ||
            !StringComparer.OrdinalIgnoreCase.Equals(previous.Value.Request.Collection, requested.Request.Collection) ||
            JsonSerializer.Serialize(previous.Value.Outcome, Json) != JsonSerializer.Serialize(requested.Outcome, Json) ||
            (previous.Value.State == CacheOperationState.Running && previous.Value.CancellationRequested && requested.State == CacheOperationState.Running && !requested.CancellationRequested) ||
            !OperationTransition(previous.Value.State, requested.State) ||
            (!cancellation && previous.Value.State == CacheOperationState.Running && previous.ClaimNode != nodeId && previous.ClaimUntil > now)))
        {
            throw new CacheTransferException(CacheErrorCode.Conflict);
        }

        if (previous is null && (expectedRevision is not null || requested.State != CacheOperationState.Queued || requested.Outcome is not null))
        {
            throw new CacheTransferException(CacheErrorCode.Conflict);
        }

        var identity = new CacheEntryIdentity(requested.Request.Namespace, requested.Request.Collection, "operation", "scope");
        var value = requested with { Collection = identity.Collection, Cutoff = requested.Cutoff.ToUniversalTime(),
            Request = requested.Request with { Namespace = identity.Namespace, Collection = identity.Collection, OperationId = requested.Id },
            Revision = cancellation ? previous.Value.Revision : Guid.NewGuid().ToString("N"), CreatedAt = previous?.Value.CreatedAt ?? now, UpdatedAt = now };
        using (var size = new CacheMetadataBuffer(previous is null ? 32768 : 65536)) { JsonSerializer.Serialize(size, value, Json); }

        var next = quota;
        if (previous is null)
        {
            next = await this.ReapOperationsAsync(transaction, next, maximumRecords, retention, true, token).ConfigureAwait(false);
            if (next.OperationRecords >= Math.Min(1000, maximumRecords)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            var collection = await this.CollectionAsync(transaction, identity.CollectionIdentity, token).ConfigureAwait(false);
            Set(transaction, CollectionKey(collection.Identity), collection.Apply(new(MetadataBytes: OperationCreditBytes), 0));
            next = next with { OperationRecords = next.OperationRecords + 1 };
        }

        var record = new CacheNativeOperationRecord(1, value, fingerprint, cancellation ? previous.ClaimNode : value.State == CacheOperationState.Running ? nodeId : null,
            cancellation ? previous.ClaimUntil : value.State == CacheOperationState.Running ? now.AddSeconds(30) : null, previous?.PendingCreditId);
        SetOperationRecord(transaction, previous, record);
        return (next, value);
    }, cancellationToken);

    /// <summary>Returns one checked tracked-operation record with an owned memory charge, or reports NotFound.</summary>
    public ValueTask<CacheMetadataDocument<CacheNativeOperationRecord>> GetOperationAsync(string id, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (transaction, quota, token) =>
        {
            using var stored = await transaction.ReadAsync(OperationKey(id), token).ConfigureAwait(false);
            if (stored is null || stored.Deleted) { throw new CacheTransferException(CacheErrorCode.NotFound); }

            if (stored.Bytes.Length > 131072 || !ResourceCharge.TryReserve(33L * stored.Bytes.Length + 65536, out var memory, this.transient, this.resident))
            {
                throw new CacheTransferException(CacheErrorCode.Capacity);
            }

            try
            {
                var record = Decode<CacheNativeOperationRecord>(stored) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                record.Validate(id);
                return (quota, new CacheMetadataDocument<CacheNativeOperationRecord>(record, stored.Bytes.Length, memory));
            }
            catch { memory.Dispose(); throw; }
        }, cancellationToken);

    /// <summary>Returns bounded authorized operations with active work first, followed by recent retained history, under a decoded-byte admission limit.</summary>
    public ValueTask<CacheNativeOperationPage> GetOperationsAsync(IReadOnlySet<string> scopes, int limit, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (transaction, quota, token) =>
        {
            var found = new List<CacheNativeOperationRecord>();
            var charges = new List<ResourceCharge>();
            long bytes = 0;
            try
            {
                foreach (var kind in new byte[] { 12, 11 })
                {
                    var range = CacheMetadataIndex.PrefixRange(new byte[] { kind });
                    await foreach (var row in transaction.ScanAsync(range.Lower, 10000, token, range.Upper).ConfigureAwait(false))
                    {
                        if (row.Deleted) { continue; }

                        if (row.Bytes.Length != 16) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                        var id = Convert.ToHexStringLower(row.Bytes.Span);
                        using var stored = await transaction.ReadAsync(OperationKey(id), token).ConfigureAwait(false);
                        if (stored is null || stored.Deleted) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                        if (stored.Bytes.Length > 131072) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                        if (stored.Bytes.Length > 4 * CacheDefaults.MiB - bytes || found.Count >= limit) { break; }

                        if (!ResourceCharge.TryReserve(33L * stored.Bytes.Length + 65536, out var memory, this.transient, this.resident))
                        {
                            throw new CacheTransferException(CacheErrorCode.Capacity);
                        }

                        charges.Add(memory);
                        var record = Decode<CacheNativeOperationRecord>(stored) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                        record.Validate(id);
                        var index = kind == 12 ? OperationStateTimeKey(record.Value) : OperationTimeKey(record.Value);
                        if (!index.Equals(row.Key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                        if (kind == 11 && !TerminalOperation(record.Value.State)) { memory.Dispose(); charges.RemoveAt(charges.Count - 1); continue; }

                        var scope = new CacheEntryIdentity(record.Value.Request.Namespace, record.Value.Request.Collection, "operation", "scope").CollectionIdentity;
                        if (!scopes.Contains(scope)) { memory.Dispose(); charges.RemoveAt(charges.Count - 1); continue; }

                        found.Add(record);
                        bytes += stored.Bytes.Length;
                    }

                    if (found.Count >= limit || bytes >= 4 * CacheDefaults.MiB) { break; }
                }

                return (quota, new CacheNativeOperationPage(Array.AsReadOnly(found.ToArray()), charges));
            }
            catch { foreach (var memory in charges) { memory.Dispose(); } throw; }
        }, cancellationToken);

    private static CacheIndexKey OperationKey(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) { throw new CacheTransferException(CacheErrorCode.Validation); }

        return new(new byte[] { 10 }.Concat(Convert.FromHexString(id)).ToArray());
    }

    private static CacheIndexKey OperationTimeKey(CacheProviderOperation operation)
    {
        Span<byte> bytes = stackalloc byte[25];
        bytes[0] = 11;
        BinaryPrimitives.WriteInt64BigEndian(bytes[1..], long.MaxValue - operation.UpdatedAt.UtcTicks);
        Convert.FromHexString(operation.Id).CopyTo(bytes[9..]);
        return new(bytes);
    }

    private static void ValidateOperationInput(CacheProviderOperation operation)
    {
        if (operation is null || !Guid.TryParseExact(operation.Id, "N", out _) || operation.Request is null || operation.Request.Continuation is not null ||
            operation.Origin?.Length > 128 || !Enum.IsDefined(operation.State) || operation.Cutoff != operation.Request.Cutoff ||
            operation.Outcome?.FailedCollections?.Count > 32)
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }
    }

    private static bool OperationTransition(CacheOperationState previous, CacheOperationState next) => previous == next || previous switch
    {
        CacheOperationState.Queued => next is CacheOperationState.Running or CacheOperationState.Paused or CacheOperationState.Cancelled or CacheOperationState.Failed,
        CacheOperationState.Running => next is CacheOperationState.Paused or CacheOperationState.Cancelled or CacheOperationState.Completed or CacheOperationState.Failed,
        CacheOperationState.Paused => next is CacheOperationState.Queued or CacheOperationState.Cancelled,
        CacheOperationState.Cancelled or CacheOperationState.Failed => next == CacheOperationState.Queued,
        _ => false
    };

    // Only CacheReaderRegistry invokes this after native reader ownership ended and both physical files disappeared.
    /// <summary>Returns an exact reader-pin credit after its caller has removed all corresponding physical pin records.</summary>
    public ValueTask ReleaseReaderAsync(string id, CacheEntryIdentity scope, CancellationToken cancellationToken) => this.ExecuteVoidAsync(
        async (transaction, quota, token) =>
        {
            using var stored = await transaction.ReadAsync(CreditKey(id), token).ConfigureAwait(false);
            var credit = Decode<CacheSpaceCredit>(stored);
            if (credit is null) { return quota; }

            ValidateCredit(credit);
            if (credit.Kind != CacheSpaceCreditKind.Reader || credit.Identity.CollectionIdentity != scope.CollectionIdentity ||
                CacheStoreRoot.Shard(credit.Identity) != CacheStoreRoot.Shard(scope) || credit.Owner != "stage-reader-" + id || credit.State != CacheSpaceCreditState.Reserved)
            {
                throw new CacheTransferException(CacheErrorCode.Corruption);
            }

            var collection = await this.CollectionAsync(transaction, credit.Identity.CollectionIdentity, token).ConfigureAwait(false);
            Set(transaction, CollectionKey(collection.Identity), collection.Apply(new(), credit.ReservedBytes));
            transaction.Set(CreditKey(id), [], deleted: true);
            return quota.Release(credit.ReservedBytes);
        }, cancellationToken);

    /// <summary>Binds one predeclared segment and manifest generation to a caller-owned reserved mutation credit.</summary>
    public ValueTask AttachCommitAsync(string id, string nodeId, CacheShardCommitIdentity commit, CancellationToken cancellationToken) =>
        this.ExecuteVoidAsync(async (transaction, quota, token) =>
        {
            var credit = await this.CreditAsync(transaction, id, token).ConfigureAwait(false);
            if (credit.NodeId != nodeId || credit.State != CacheSpaceCreditState.Reserved || commit is null ||
                !Guid.TryParseExact(commit.SegmentId, "N", out _) || !Guid.TryParseExact(commit.Generation, "N", out _) ||
                (credit.Commit is not null && credit.Commit != commit))
            {
                throw new CacheTransferException(CacheErrorCode.Conflict);
            }

            Set(transaction, CreditKey(id), credit with { Commit = commit });
            return quota;
        }, cancellationToken);

    // The caller already holds its collection shard authority and has checked exact version/fence/owner.
    // Root authority spans durable preparation, the single shard namespace publication, and final accounting.
    /// <summary>Prepares durable quota and the shard receipt, runs the guarded manifest publication, then confirms its quota delta and optional operation checkpoint exactly once.</summary>
    public ValueTask<long> PublishAsync(string id, string nodeId, long expectedEpoch, CacheQuotaChange change,
        Func<CancellationToken, ValueTask<long>> publish, CancellationToken cancellationToken, long publicationSequence = 0,
        string expectedPreviousGeneration = null, long? expectedPreviousSequence = null, CacheNativeTrackedProgress trackedProgress = null) =>
        this.ExecuteAsync(async (transaction, quota, token) =>
        {
            var credit = await this.CreditAsync(transaction, id, token).ConfigureAwait(false);
            var collection = await this.CollectionAsync(transaction, credit.Identity.CollectionIdentity, token).ConfigureAwait(false);
            if (credit.State != CacheSpaceCreditState.Reserved || credit.NodeId != nodeId || credit.Commit is null || collection.Epoch != expectedEpoch || publicationSequence < 0)
            {
                throw new CacheTransferException(CacheErrorCode.Conflict);
            }

            var previousReceipt = await this.ReceiptAsync(transaction, credit.Identity, token).ConfigureAwait(false);
            if ((previousReceipt is not null && (previousReceipt.State != CacheShardReceiptState.Committed ||
                    (expectedPreviousGeneration is not null && previousReceipt.Generation != expectedPreviousGeneration) ||
                    (expectedPreviousSequence.HasValue && previousReceipt.Sequence != expectedPreviousSequence.Value))) ||
                (previousReceipt is null && expectedPreviousSequence.HasValue && expectedPreviousSequence.Value != 0))
            {
                throw new CacheTransferException(CacheErrorCode.ProviderUnavailable);
            }

            var preparedRoot = quota.Prepare(change);
            var preparedCollection = collection.Prepare(change);
            _ = preparedRoot.Confirm(change, credit.ReservedBytes); // Validate the complete outcome before making it visible.
            var confirmedCollection = preparedCollection.Confirm(change, credit.ReservedBytes);
            if (trackedProgress is not null) { await this.PrepareTrackedProgressAsync(transaction, trackedProgress, credit.Id, token).ConfigureAwait(false); }

            var preparedCredit = credit with { State = CacheSpaceCreditState.Prepared, PreparedChange = change, PublicationGeneration = credit.Commit.Generation,
                TrackedProgress = trackedProgress };
            var receipt = new CacheShardReceipt(1, this.header.StoreId, collection.Identity, CacheStoreRoot.Shard(credit.Identity), credit.Id,
                credit.Commit.Generation, publicationSequence, CacheShardReceiptState.Prepared);
            Set(transaction, CollectionKey(collection.Identity), preparedCollection);
            Set(transaction, CreditKey(id), preparedCredit);
            Set(transaction, ReceiptKey(credit.Identity), receipt);
            preparedRoot = await this.PersistAsync(transaction, preparedRoot, token).ConfigureAwait(false);
            await transaction.RefreshAfterCommitAsync(token).ConfigureAwait(false);

            long bytes;
            try { bytes = await publish(token).ConfigureAwait(false); }
            catch { transaction.Authority.RetainIfShared(); throw; }

            Set(transaction, CollectionKey(collection.Identity), confirmedCollection);
            Set(transaction, CreditKey(id), preparedCredit with { State = CacheSpaceCreditState.Committed, ReservedBytes = 0 });
            Set(transaction, ReceiptKey(credit.Identity), receipt with { State = CacheShardReceiptState.Committed });
            if (trackedProgress is not null) { await this.ConfirmTrackedProgressAsync(transaction, trackedProgress, credit.Id, token).ConfigureAwait(false); }

            return (preparedRoot.Confirm(change, credit.ReservedBytes), bytes);
        }, cancellationToken);

    // A cooperative caller supplies this proof only after checking the exact committed payload/version
    // under its shard authority. No application-supplied identifier is sufficient proof by itself.
    /// <summary>Confirms a prepared quota delta only for the exact identity, payload version and observed publication generation; repeated confirmations are idempotent.</summary>
    public ValueTask<CacheSpaceCredit> ConfirmPreparedAsync(string id, CacheEntryIdentity identity, string version, string publicationGeneration,
        CancellationToken cancellationToken) => this.ExecuteAsync(async (transaction, quota, token) =>
        {
            var credit = await this.CreditAsync(transaction, id, token).ConfigureAwait(false);
            if (credit.Identity != identity || credit.Version != version || credit.PublicationGeneration != publicationGeneration)
            {
                throw new CacheTransferException(CacheErrorCode.Conflict);
            }

            if (credit.State == CacheSpaceCreditState.Committed) { return (quota, credit); }

            if (credit.State != CacheSpaceCreditState.Prepared || credit.PreparedChange is null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            var collection = await this.CollectionAsync(transaction, identity.CollectionIdentity, token).ConfigureAwait(false);
            var receipt = await this.ReceiptAsync(transaction, identity, token).ConfigureAwait(false);
            if (receipt is null || receipt.CreditId != credit.Id || receipt.Generation != publicationGeneration || receipt.State != CacheShardReceiptState.Prepared)
            {
                throw new CacheTransferException(CacheErrorCode.Conflict);
            }

            var next = quota.Confirm(credit.PreparedChange, credit.ReservedBytes);
            Set(transaction, CollectionKey(collection.Identity), collection.Confirm(credit.PreparedChange, credit.ReservedBytes));
            credit = credit with { State = CacheSpaceCreditState.Committed, ReservedBytes = 0 };
            Set(transaction, CreditKey(id), credit);
            Set(transaction, ReceiptKey(identity), receipt with { State = CacheShardReceiptState.Committed });
            if (credit.TrackedProgress is not null) { await this.ConfirmTrackedProgressAsync(transaction, credit.TrackedProgress, credit.Id, token).ConfigureAwait(false); }

            return (next, credit);
        }, cancellationToken);

    // Physical cleanup and actual owner termination must be established by the native caller first.
    /// <summary>Returns a proven unpublished reservation or aborts its prepared attribution while retaining any already-owned physical metadata charge.</summary>
    public ValueTask ReleaseUnpublishedAsync(string id, string nodeId, CancellationToken cancellationToken, long retainedMetadataBytes = 0) =>
        this.ExecuteVoidAsync(async (transaction, quota, token) =>
        {
            var credit = await this.CreditAsync(transaction, id, token).ConfigureAwait(false);
            if (credit.NodeId != nodeId || credit.State == CacheSpaceCreditState.Committed) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            if (credit.State == CacheSpaceCreditState.Released) { return quota; }

            var collection = await this.CollectionAsync(transaction, credit.Identity.CollectionIdentity, token).ConfigureAwait(false);
            CacheRootQuotaState next;
            if (credit.State == CacheSpaceCreditState.Prepared)
            {
                if (credit.PreparedChange is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                next = quota.AbortPrepared(credit.PreparedChange, credit.ReservedBytes);
                collection = collection.AbortPrepared(credit.PreparedChange, credit.ReservedBytes);
                var receipt = await this.ReceiptAsync(transaction, credit.Identity, token).ConfigureAwait(false);
                if (receipt is null || receipt.CreditId != credit.Id || receipt.State != CacheShardReceiptState.Prepared)
                {
                    throw new CacheTransferException(CacheErrorCode.Conflict);
                }

                Set(transaction, ReceiptKey(credit.Identity), receipt with { State = CacheShardReceiptState.Aborted });
            }
            else
            {
                next = quota.Release(credit.ReservedBytes);
                collection = collection.Apply(new(), credit.ReservedBytes);
            }

            if (retainedMetadataBytes < 0 || retainedMetadataBytes > credit.ReservedBytes) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            next = next.Apply(new(MetadataBytes: retainedMetadataBytes), 0);
            collection = collection.Apply(new(MetadataBytes: retainedMetadataBytes), 0);
            Set(transaction, CollectionKey(collection.Identity), collection);
            Set(transaction, CreditKey(id), credit with { State = CacheSpaceCreditState.Released, ReservedBytes = 0 });
            return next;
        }, cancellationToken);

    /// <summary>Checks a selection against every retained contract schema in the logical collection before any candidate scan begins.</summary>
    public ValueTask<IReadOnlyList<CacheDimensionSchema>> ValidateSelectionAsync(CacheEntryIdentity scope, CacheSelection selection, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (transaction, quota, token) =>
        {
            ArgumentNullException.ThrowIfNull(selection);
            var prefix = new byte[] { 9 }.Concat(Convert.FromHexString(CacheStoreRoot.Digest(scope.CollectionIdentity))).ToArray();
            var range = CacheMetadataIndex.PrefixRange(prefix);
            IReadOnlyList<CacheDimensionSchema> first = null;
            var count = 0;
            await foreach (var row in transaction.ScanAsync(range.Lower, 1025, token).ConfigureAwait(false))
            {
                if (!range.Contains(row.Key)) { break; }

                if (++count > 1024) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                if (row.Deleted) { continue; }

                var reference = JsonSerializer.Deserialize<CacheNativeContractReference>(row.Bytes.Span, Json) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                if (reference.Format != 1 || reference.Identity.CollectionIdentity != scope.CollectionIdentity ||
                    !ContractReferenceKey(reference.Identity).Equals(row.Key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                using var stored = await transaction.ReadAsync(ContractKey(reference.Identity), token).ConfigureAwait(false);
                var contract = Decode<CacheNativeContract>(stored) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                contract.Validate(reference.Identity);
                if (CacheSelectionValidator.Validate(selection, contract.Dimensions).IsFailure) { throw new CacheTransferException(CacheErrorCode.Validation); }

                first ??= contract.Dimensions.ToArray();
            }

            if (first is null) { throw new CacheTransferException(CacheErrorCode.Compatibility); }

            return (quota, first);
        }, cancellationToken);

    /// <summary>Captures the durable epoch and per-shard publication sequence bounds for one logical collection.</summary>
    public ValueTask<CacheCollectionBoundary> CaptureBoundaryAsync(CacheEntryIdentity scope, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (transaction, quota, token) =>
            (quota, await this.ReadBoundaryAsync(transaction, scope, token).ConfigureAwait(false)), cancellationToken);

    private async ValueTask<CacheCollectionBoundary> ReadBoundaryAsync(CacheShardTransaction transaction, CacheEntryIdentity scope, CancellationToken token)
    {
            var collection = await this.CollectionAsync(transaction, scope.CollectionIdentity, token).ConfigureAwait(false);
            var maximum = new long[256];
            var prefix = new byte[] { 7 }.Concat(Convert.FromHexString(CacheStoreRoot.Digest(scope.CollectionIdentity))).ToArray();
            var range = CacheMetadataIndex.PrefixRange(prefix);
            var count = 0;
            await foreach (var row in transaction.ScanAsync(range.Lower, 257, token).ConfigureAwait(false))
            {
                if (!range.Contains(row.Key)) { break; }

                if (++count > 256 || row.Deleted) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                var shard = row.Key.Bytes[33];
                var receipt = JsonSerializer.Deserialize<CacheShardReceipt>(row.Bytes.Span, Json) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                receipt.ValidateScope(this.header.StoreId, scope.CollectionIdentity, shard);
                if (!ReceiptKey(scope.CollectionIdentity, shard).Equals(row.Key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                maximum[shard] = receipt.Sequence;
            }

            return new CacheCollectionBoundary(collection.Epoch, maximum);
    }

    /// <summary>Reads bounded retained catalog identities for explicit offline recovery without deriving ownership from a filesystem payload inventory.</summary>
    public ValueTask<IReadOnlyList<CacheEntryIdentity>> GetRecoveryScopesAsync(CancellationToken cancellationToken) =>
        this.ExecuteAsync<IReadOnlyList<CacheEntryIdentity>>(async (transaction, quota, token) =>
        {
            var scopes = new Dictionary<string, CacheEntryIdentity>(StringComparer.Ordinal);
            var range = CacheMetadataIndex.PrefixRange(new byte[] { 4 });
            var count = 0;
            await foreach (var row in transaction.ScanAsync(range.Lower, 1025, token, range.Upper).ConfigureAwait(false))
            {
                if (++count > 1024) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                if (row.Deleted) { continue; }

                if (row.Bytes.Length > 65536 || !ResourceCharge.TryReserve(33L * row.Bytes.Length + 65536, out var memory, this.transient, this.resident))
                {
                    throw new CacheTransferException(CacheErrorCode.Capacity);
                }

                using (memory)
                {
                    var contract = JsonSerializer.Deserialize<CacheNativeContract>(row.Bytes.Span, Json) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                    contract.Validate(contract.Identity);
                    if (!ContractKey(contract.Identity).Equals(row.Key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                    scopes.TryAdd(contract.Identity.CollectionIdentity, contract.Identity);
                }
            }

            return (quota, Array.AsReadOnly(scopes.Values.ToArray()));
        }, cancellationToken);

    private const string RecoveryCursorPurpose = "BDK.ApplicationCaching.FileSystem.Cursor.V1";

    /// <summary>Reencrypts active tracked continuations for the fenced recovery generation and clears stopped execution claims while preserving their original cutoff and sequence bounds.</summary>
    public async ValueTask RebindRecoveryOperationsAsync(CacheStoreHeader fence, byte[] secret, CancellationToken cancellationToken)
    {
        CacheIndexKey after = null;
        do { after = await this.RebindOperationPageAsync(fence, secret, after, cancellationToken).ConfigureAwait(false); }
        while (after is not null);
    }

    private ValueTask<CacheIndexKey> RebindOperationPageAsync(CacheStoreHeader fence, byte[] secret, CacheIndexKey after,
        CancellationToken cancellationToken) => this.ExecuteAsync(async (transaction, quota, token) =>
        {
            var range = CacheMetadataIndex.PrefixRange(new byte[] { 10 });
            var selected = new List<CacheIndexKey>();
            CacheIndexKey last = null;
            var examined = 0;
            await foreach (var row in transaction.ScanAsync(after?.After() ?? range.Lower, 16, token, range.Upper).ConfigureAwait(false))
            {
                examined++;
                last = row.Key;
                if (!row.Deleted) { selected.Add(row.Key); }
            }

            foreach (var key in selected)
            {
                using var stored = await transaction.ReadAsync(key, token).ConfigureAwait(false);
                if (stored is null || stored.Deleted || stored.Bytes.Length > 131072) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                if (!ResourceCharge.TryReserve(33L * stored.Bytes.Length + 65536, out var memory, this.transient, this.resident))
                {
                    throw new CacheTransferException(CacheErrorCode.Capacity);
                }

                using (memory)
                {
                    var record = Decode<CacheNativeOperationRecord>(stored) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                    record.Validate(record.Value.Id);
                    if (!OperationKey(record.Value.Id).Equals(key) || record.PendingCreditId is not null) { throw new CacheTransferException(CacheErrorCode.Indeterminate); }

                    if (TerminalOperation(record.Value.State)) { continue; }

                    var outcome = record.Value.Outcome;
                    if (outcome?.Continuation is not null)
                    {
                        outcome = outcome with { Continuation = RebindContinuation(record, fence, secret) };
                    }

                    var value = record.Value with { Outcome = outcome, State = record.Value.State == CacheOperationState.Running ? CacheOperationState.Pending : record.Value.State,
                        Revision = Guid.NewGuid().ToString("N"), UpdatedAt = this.time.GetUtcNow() };
                    SetOperationRecord(transaction, record, record with { Value = value, ClaimNode = null, ClaimUntil = null });
                }
            }

            return (quota, examined < 16 ? null : last);
        }, cancellationToken);

    private static string RebindContinuation(CacheNativeOperationRecord operation, CacheStoreHeader fence, byte[] secret)
    {
        try
        {
            var encoded = operation.Value.Outcome.Continuation;
            if (encoded.Length > 32768) { throw new InvalidDataException(); }

            var plaintext = AesGcmContinuationTokenProtector.Unprotect(Convert.FromBase64String(encoded), secret, RecoveryCursorPurpose);
            var cursor = JsonSerializer.Deserialize<CacheFileQueryCursor>(plaintext, Json) ?? throw new InvalidDataException();
            var scope = new CacheEntryIdentity(operation.Value.Request.Namespace, operation.Value.Request.Collection, "operation", "scope");
            var purpose = (operation.Value.Request.Clear ? "clear:" : "invalidate:") + operation.Value.Id;
            if (cursor.Format != 1 || cursor.StoreId != fence.StoreId ||
                (cursor.RecoveryGeneration != fence.RecoveryBaseGeneration && cursor.RecoveryGeneration != fence.RecoveryGeneration) ||
                cursor.Scope != scope.CollectionIdentity || cursor.SelectionHash != operation.SelectionHash || cursor.CutoffTicks != operation.Value.Cutoff.UtcTicks ||
                cursor.Purpose != purpose || cursor.Shard is < 0 or > 256 || cursor.Epoch < 0 || cursor.MaximumSequence is null || cursor.MaximumSequence.Count != 256 ||
                cursor.MaximumSequence.Any(sequence => sequence < 0) || cursor.Observed < 0 || cursor.Examined < 0 || cursor.MaximumCandidates is <= 0 or > 100000 ||
                (cursor.LastKey is not null && (Convert.FromBase64String(cursor.LastKey).Length != CacheIndexKey.Size || cursor.Shard == 256)))
            {
                throw new InvalidDataException();
            }

            using var buffer = new CacheMetadataBuffer(AesGcmContinuationTokenProtector.MaximumPlaintextBytes);
            JsonSerializer.Serialize(buffer, cursor with { RecoveryGeneration = fence.RecoveryGeneration, Changed = true }, Json);
            return Convert.ToBase64String(AesGcmContinuationTokenProtector.Protect(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), secret, RecoveryCursorPurpose));
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException or JsonException or CryptographicException or ArgumentException)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    /// <summary>Atomically captures the bounded collection invalidation outcome and, when requested, advances the root collection epoch before scanning shards.</summary>
    public ValueTask<CacheInvalidationOutcome> BeginInvalidationAsync(CacheEntryIdentity scope, string fingerprint, string operationId,
        string nodeId, Func<CacheCollectionBoundary, CacheInvalidationOutcome> create, CancellationToken cancellationToken, bool advance = true) =>
        this.ExecuteAsync(async (transaction, quota, token) =>
        {
            var boundary = await this.ReadBoundaryAsync(transaction, scope, token).ConfigureAwait(false);
            var collection = await this.CollectionAsync(transaction, scope.CollectionIdentity, token).ConfigureAwait(false);
            var nextBoundary = boundary with { Epoch = advance ? checked(collection.Epoch + 1) : collection.Epoch };
            var outcome = create(nextBoundary);
            if (operationId is not null)
            {
                using var stored = await transaction.ReadAsync(OperationKey(operationId), token).ConfigureAwait(false);
                var operation = Decode<CacheNativeOperationRecord>(stored) ?? throw new CacheTransferException(CacheErrorCode.NotFound);
                ValidateTracked(operation, operationId, fingerprint, nodeId, null, this.time.GetUtcNow());
                if (operation.Value.Outcome is not null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

                this.SetProgress(transaction, operation, outcome, Guid.NewGuid().ToString("N"));
            }

            Set(transaction, CollectionKey(collection.Identity), collection with { Epoch = nextBoundary.Epoch });
            return (quota, outcome);
        }, cancellationToken);

    /// <summary>Validates the operation claim, fingerprint, revision and continuation before creating one proposed durable progress checkpoint.</summary>
    public ValueTask<CacheNativeTrackedProgress> CreateTrackedProgressAsync(string id, string fingerprint, string nodeId, string continuation,
        CacheInvalidationOutcome outcome, CancellationToken cancellationToken) => this.ExecuteAsync(async (transaction, quota, token) =>
        {
            using var stored = await transaction.ReadAsync(OperationKey(id), token).ConfigureAwait(false);
            var operation = Decode<CacheNativeOperationRecord>(stored) ?? throw new CacheTransferException(CacheErrorCode.NotFound);
            ValidateTracked(operation, id, fingerprint, nodeId, continuation, this.time.GetUtcNow());
            return (quota, new CacheNativeTrackedProgress(id, operation.Value.Revision, continuation, outcome, nodeId, Guid.NewGuid().ToString("N")));
        }, cancellationToken);

    /// <summary>Compare-and-swaps one checked progress checkpoint without replaying its already-observed shard effects.</summary>
    public ValueTask SaveTrackedProgressAsync(CacheNativeTrackedProgress progress, CancellationToken cancellationToken) =>
        this.ExecuteVoidAsync(async (transaction, quota, token) =>
        {
            await this.PrepareTrackedProgressAsync(transaction, progress, null, token).ConfigureAwait(false);
            return quota;
        }, cancellationToken);

    private async ValueTask PrepareTrackedProgressAsync(CacheShardTransaction transaction, CacheNativeTrackedProgress progress,
        string creditId, CancellationToken token)
    {
        using var stored = await transaction.ReadAsync(OperationKey(progress.OperationId), token).ConfigureAwait(false);
        var operation = Decode<CacheNativeOperationRecord>(stored) ?? throw new CacheTransferException(CacheErrorCode.Conflict);
        operation.Validate(progress.OperationId);
        if (operation.PendingCreditId is not null || operation.Value.Revision != progress.ExpectedRevision ||
            operation.Value.State != CacheOperationState.Running || operation.ClaimNode != progress.NodeId || operation.ClaimUntil <= this.time.GetUtcNow() ||
            operation.Value.Outcome?.Continuation != progress.ExpectedContinuation) { throw new CacheTransferException(CacheErrorCode.Conflict); }

        if (creditId is null) { this.SetProgress(transaction, operation, progress.Outcome, progress.Revision); }
        else { Set(transaction, OperationKey(progress.OperationId), operation with { PendingCreditId = creditId }); }
    }

    private async ValueTask ConfirmTrackedProgressAsync(CacheShardTransaction transaction, CacheNativeTrackedProgress progress,
        string creditId, CancellationToken token)
    {
        using var stored = await transaction.ReadAsync(OperationKey(progress.OperationId), token).ConfigureAwait(false);
        var operation = Decode<CacheNativeOperationRecord>(stored) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
        operation.Validate(progress.OperationId);
        if (operation.PendingCreditId != creditId || operation.Value.Revision != progress.ExpectedRevision ||
            operation.Value.Outcome?.Continuation != progress.ExpectedContinuation) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        this.SetProgress(transaction, operation, progress.Outcome, progress.Revision);
    }

    private void SetProgress(CacheShardTransaction transaction, CacheNativeOperationRecord previous, CacheInvalidationOutcome outcome, string revision)
    {
        var now = this.time.GetUtcNow();
        var value = previous.Value with { Outcome = outcome, Revision = revision, UpdatedAt = now,
            State = outcome.Completed ? CacheOperationState.Completed : previous.Value.CancellationRequested ? CacheOperationState.Cancelled : CacheOperationState.Running };
        using (var size = new CacheMetadataBuffer(65536)) { System.Text.Json.JsonSerializer.Serialize(size, value, Json); }

        SetOperationRecord(transaction, previous, previous with { Value = value, PendingCreditId = null,
            ClaimUntil = value.State == CacheOperationState.Running ? now.AddSeconds(30) : null,
            ClaimNode = value.State == CacheOperationState.Running ? previous.ClaimNode : null });
    }

    private static void ValidateTracked(CacheNativeOperationRecord operation, string id, string fingerprint, string nodeId,
        string continuation, DateTimeOffset now)
    {
        operation.Validate(id);
        if (operation.PendingCreditId is not null || operation.Value.State != CacheOperationState.Running || operation.ClaimNode != nodeId ||
            operation.ClaimUntil <= now || operation.SelectionHash != fingerprint || operation.Value.Outcome?.Continuation != continuation)
        {
            throw new CacheTransferException(CacheErrorCode.Conflict);
        }
    }

    /// <summary>Observes a remote cancellation request at a proven operation boundary and durably completes cancellation only for the current execution claim.</summary>
    public ValueTask<CacheInvalidationOutcome> ObserveCancellationBoundaryAsync(string id, string continuation, string nodeId,
        CancellationToken cancellationToken) => this.ExecuteAsync(async (transaction, quota, token) =>
        {
            using var stored = await transaction.ReadAsync(OperationKey(id), token).ConfigureAwait(false);
            var operation = Decode<CacheNativeOperationRecord>(stored) ?? throw new CacheTransferException(CacheErrorCode.NotFound);
            operation.Validate(id);
            if (!operation.Value.CancellationRequested) { return (quota, (CacheInvalidationOutcome)null); }

            if (operation.PendingCreditId is not null) { throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

            if (operation.Value.State == CacheOperationState.Cancelled) { return (quota, operation.Value.Outcome ?? new() { OperationId = id }); }

            if (operation.Value.State != CacheOperationState.Running || operation.ClaimNode != nodeId || operation.ClaimUntil <= this.time.GetUtcNow() ||
                operation.Value.Outcome?.Continuation != continuation) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            var outcome = operation.Value.Outcome ?? new CacheInvalidationOutcome { OperationId = id };
            this.SetProgress(transaction, operation, outcome, Guid.NewGuid().ToString("N"));
            return (quota, outcome);
        }, cancellationToken);

    /// <summary>Reads the durable publication receipt for an identity&apos;s shard or an explicitly supplied shard within a logical collection.</summary>
    public ValueTask<CacheShardReceipt> GetReceiptAsync(CacheEntryIdentity identity, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (transaction, quota, token) => (quota, await this.ReceiptAsync(transaction, identity, token).ConfigureAwait(false)), cancellationToken);

    /// <summary>Reads the durable publication receipt for an identity&apos;s shard or an explicitly supplied shard within a logical collection.</summary>
    public ValueTask<CacheShardReceipt> GetReceiptAsync(CacheEntryIdentity scope, int shard, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (transaction, quota, token) => (quota, await this.ReceiptAsync(transaction, scope.CollectionIdentity, shard, token).ConfigureAwait(false)), cancellationToken);

    private async ValueTask<CacheShardReceipt> ReceiptAsync(CacheShardTransaction transaction, CacheEntryIdentity identity, CancellationToken token)
        => await this.ReceiptAsync(transaction, identity.CollectionIdentity, CacheStoreRoot.Shard(identity), token).ConfigureAwait(false);

    private async ValueTask<CacheShardReceipt> ReceiptAsync(CacheShardTransaction transaction, string collectionIdentity, int shard, CancellationToken token)
    {
        using var stored = await transaction.ReadAsync(ReceiptKey(collectionIdentity, shard), token).ConfigureAwait(false);
        var receipt = Decode<CacheShardReceipt>(stored);
        receipt?.ValidateScope(this.header.StoreId, collectionIdentity, shard);
        return receipt;
    }

    private static CacheIndexKey ReceiptKey(CacheEntryIdentity identity) => ReceiptKey(identity.CollectionIdentity, CacheStoreRoot.Shard(identity));

    private static CacheIndexKey ReceiptKey(string collectionIdentity, int shard) => new(new byte[] { 7 }.Concat(Convert.FromHexString(
        CacheStoreRoot.Digest(collectionIdentity))).Append((byte)shard).ToArray());
}