// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Holds conservative store-wide quota attribution and prepared-publication counters persisted in the root control ledger.</summary>
/// <example><code>state.Validate(storeId); var snapshot = state.Snapshot(controlBytes);</code></example>
public sealed record CacheRootQuotaState
{
    /// <summary>Gets the durable quota-record format, currently one.</summary>
    public int Format { get; init; } = 1;

    /// <summary>Gets the generated store identity owning this root quota state.</summary>
    public string StoreId { get; init; }

    /// <summary>Gets the fixed maximum physical byte attribution admitted by this quota.</summary>
    public long MaximumBytes { get; init; }

    /// <summary>Gets the maximum total current and prepared entry count admitted by this quota.</summary>
    public long MaximumEntries { get; init; }

    /// <summary>Gets bytes attributed to current published payloads.</summary>
    public long CurrentBytes { get; init; }

    /// <summary>Gets bytes attributed to retired payloads still awaiting safe physical reclamation.</summary>
    public long RetiredBytes { get; init; }

    /// <summary>Gets bytes durably admitted to physical operations whose final attribution is not yet confirmed.</summary>
    public long ReservedBytes { get; init; }

    /// <summary>Gets conservative bytes attributed to checked metadata and index publications.</summary>
    public long MetadataBytes { get; init; }

    /// <summary>Gets the count of currently published positive and negative entries.</summary>
    public long CurrentEntries { get; init; }

    /// <summary>Gets the dedicated control-ledger workspace charged before ordinary data admission.</summary>
    public long WorkspaceBytes { get; init; }

    /// <summary>Gets positive entry-count deltas reserved by publications awaiting exact confirmation.</summary>
    public long PreparedEntries { get; init; }

    /// <summary>Gets the count of prepared publications whose final quota deltas remain unresolved.</summary>
    public long PreparedOperations { get; init; }

    /// <summary>Gets the retained contract count bounded by the format limit of 1,024.</summary>
    public int CatalogContracts { get; init; }

    /// <summary>Gets the retained tracked-operation count bounded by the configured history limit.</summary>
    public int OperationRecords { get; init; }

    /// <summary>Gets remaining unreserved bytes after current, retired, metadata and dedicated control-workspace attribution, or a negative value for inconsistent counters.</summary>
    public long Available => Remaining(this.MaximumBytes, [this.CurrentBytes, this.RetiredBytes, this.ReservedBytes, this.MetadataBytes, this.WorkspaceBytes]);

    /// <summary>Rejects identity mismatches, impossible quota counters and bounded catalog or operation counts outside the format contract.</summary>
    public void Validate(string storeId)
    {
        if (this.Format != 1 || this.StoreId != storeId || !Guid.TryParseExact(this.StoreId, "N", out _) || this.MaximumBytes <= 0 || this.MaximumEntries <= 0 ||
            this.CurrentEntries < 0 || this.CurrentEntries > this.MaximumEntries || this.PreparedEntries < 0 || this.PreparedEntries > this.MaximumEntries - this.CurrentEntries ||
            this.PreparedOperations < 0 || this.CatalogContracts is < 0 or > 1024 || this.OperationRecords is < 0 or > 1000 || this.WorkspaceBytes <= 0 || this.Available < 0)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    /// <summary>Returns a state charging additional positive reserved bytes, or rejects capacity before attribution changes.</summary>
    public CacheRootQuotaState Reserve(long bytes)
    {
        if (bytes <= 0 || bytes > this.Available) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        return this with { ReservedBytes = this.ReservedBytes + bytes };
    }

    /// <summary>Returns a state releasing at most the bytes currently reserved; inconsistent releases report Corruption.</summary>
    public CacheRootQuotaState Release(long bytes)
    {
        if (bytes < 0 || bytes > this.ReservedBytes) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        return this with { ReservedBytes = this.ReservedBytes - bytes };
    }

    /// <summary>Releases the supplied reservation and applies one checked quota delta, rejecting overflow or exceeded store limits.</summary>
    public CacheRootQuotaState Apply(CacheQuotaChange change, long releasedCredit)
    {
        var result = this.Release(releasedCredit) with { CurrentBytes = Add(this.CurrentBytes, change.CurrentBytes), RetiredBytes = Add(this.RetiredBytes, change.RetiredBytes),
            MetadataBytes = Add(this.MetadataBytes, change.MetadataBytes), CurrentEntries = Add(this.CurrentEntries, change.Entries) };
        if (result.Available < 0 || result.CurrentEntries > result.MaximumEntries || result.PreparedEntries > result.MaximumEntries - result.CurrentEntries)
        {
            throw new CacheTransferException(CacheErrorCode.Capacity);
        }

        return result;
    }

    /// <summary>Charges prepared entry and operation counters before publication without yet adopting the final quota delta.</summary>
    public CacheRootQuotaState Prepare(CacheQuotaChange change)
    {
        var result = this with { PreparedEntries = Add(this.PreparedEntries, Math.Max(0, change.Entries)), PreparedOperations = Add(this.PreparedOperations, 1) };
        if (result.PreparedEntries > result.MaximumEntries - result.CurrentEntries) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        return result;
    }

    /// <summary>Removes the prepared counters and applies the publication delta while returning its reserved credit exactly once.</summary>
    public CacheRootQuotaState Confirm(CacheQuotaChange change, long releasedCredit) =>
        (this with { PreparedEntries = Add(this.PreparedEntries, -Math.Max(0, change.Entries)), PreparedOperations = Add(this.PreparedOperations, -1) }).Apply(change, releasedCredit);

    /// <summary>Removes prepared counters and returns reserved space for a publication proven not to have committed.</summary>
    public CacheRootQuotaState AbortPrepared(CacheQuotaChange change, long releasedCredit) =>
        (this with { PreparedEntries = Add(this.PreparedEntries, -Math.Max(0, change.Entries)), PreparedOperations = Add(this.PreparedOperations, -1) }).Release(releasedCredit);

    /// <summary>Maps conservative attribution to public quota fields, separating actual control bytes from unused dedicated workspace.</summary>
    public CacheProviderQuota Snapshot(long workspaceActualBytes)
    {
        if (workspaceActualBytes < 0 || workspaceActualBytes > this.WorkspaceBytes) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        return new(this.CurrentBytes, this.RetiredBytes, this.ReservedBytes + this.WorkspaceBytes - workspaceActualBytes,
            this.MetadataBytes + workspaceActualBytes, this.CurrentEntries + this.PreparedEntries, this.MaximumBytes, this.PreparedOperations == 0);
    }

    /// <summary>Subtracts nonnegative bounded components without overflow, returning negative one for inconsistent attribution.</summary>
    public static long Remaining(long maximum, ReadOnlySpan<long> parts)
    {
        foreach (var part in parts)
        {
            if (part < 0 || part > maximum) { return -1; }

            maximum -= part;
        }

        return maximum;
    }

    /// <summary>Adds a checked quota delta without allowing negative counters or integer overflow.</summary>
    public static long Add(long current, long delta)
    {
        if (current < 0 || (delta < 0 && delta < -current) || (delta > 0 && delta > long.MaxValue - current))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        return current + delta;
    }
}

/// <summary>Holds one logical collection&apos;s durable quotas, invalidation epoch and conservative payload, metadata and reservation attribution.</summary>
/// <example><code>state.Validate(collectionIdentity); var available = state.Available;</code></example>
public sealed record CacheCollectionQuotaState
{
    /// <summary>Gets the canonical logical collection identity shared across its value contracts.</summary>
    public string Identity { get; init; }

    /// <summary>Gets the fixed maximum physical byte attribution admitted by this quota.</summary>
    public long MaximumBytes { get; init; }

    /// <summary>Gets the maximum total current and prepared entry count admitted by this quota.</summary>
    public long MaximumEntries { get; init; }

    /// <summary>Gets the durable collection boundary that invalidates earlier source publication rights.</summary>
    public long Epoch { get; init; }

    /// <summary>Gets bytes attributed to current published payloads.</summary>
    public long CurrentBytes { get; init; }

    /// <summary>Gets bytes attributed to retired payloads still awaiting safe physical reclamation.</summary>
    public long RetiredBytes { get; init; }

    /// <summary>Gets bytes durably admitted to physical operations whose final attribution is not yet confirmed.</summary>
    public long ReservedBytes { get; init; }

    /// <summary>Gets conservative bytes attributed to checked metadata and index publications.</summary>
    public long MetadataBytes { get; init; }

    /// <summary>Gets the count of currently published positive and negative entries.</summary>
    public long CurrentEntries { get; init; }

    /// <summary>Gets positive entry-count deltas reserved by publications awaiting exact confirmation.</summary>
    public long PreparedEntries { get; init; }

    /// <summary>Gets remaining collection bytes after current, retired, reserved and metadata attribution.</summary>
    public long Available => CacheRootQuotaState.Remaining(this.MaximumBytes, [this.CurrentBytes, this.RetiredBytes, this.ReservedBytes, this.MetadataBytes]);

    /// <summary>Rejects another collection&apos;s state, impossible quotas, negative epochs and counters exceeding the configured entry or byte limits.</summary>
    public void Validate(string identity)
    {
        if (this.Identity != identity || string.IsNullOrEmpty(identity) || this.MaximumBytes <= 0 || this.MaximumEntries <= 0 || this.Epoch < 0 ||
            this.CurrentEntries < 0 || this.CurrentEntries > this.MaximumEntries || this.PreparedEntries < 0 ||
            this.PreparedEntries > this.MaximumEntries - this.CurrentEntries || this.Available < 0)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    /// <summary>Charges positive reserved bytes within the remaining logical collection quota.</summary>
    public CacheCollectionQuotaState Reserve(long bytes)
    {
        if (bytes <= 0 || bytes > this.Available) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        return this with { ReservedBytes = this.ReservedBytes + bytes };
    }

    /// <summary>Returns reserved credit and applies checked byte and entry deltas within the collection&apos;s limits.</summary>
    public CacheCollectionQuotaState Apply(CacheQuotaChange change, long releasedCredit)
    {
        if (releasedCredit < 0 || releasedCredit > this.ReservedBytes) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        var result = this with { ReservedBytes = this.ReservedBytes - releasedCredit, CurrentBytes = CacheRootQuotaState.Add(this.CurrentBytes, change.CurrentBytes),
            RetiredBytes = CacheRootQuotaState.Add(this.RetiredBytes, change.RetiredBytes), MetadataBytes = CacheRootQuotaState.Add(this.MetadataBytes, change.MetadataBytes),
            CurrentEntries = CacheRootQuotaState.Add(this.CurrentEntries, change.Entries) };
        if (result.Available < 0 || result.CurrentEntries > result.MaximumEntries || result.PreparedEntries > result.MaximumEntries - result.CurrentEntries)
        {
            throw new CacheTransferException(CacheErrorCode.Capacity);
        }

        return result;
    }

    /// <summary>Adds the positive entry delta to prepared attribution before the corresponding shard publication.</summary>
    public CacheCollectionQuotaState Prepare(CacheQuotaChange change)
    {
        var result = this with { PreparedEntries = CacheRootQuotaState.Add(this.PreparedEntries, Math.Max(0, change.Entries)) };
        if (result.PreparedEntries > result.MaximumEntries - result.CurrentEntries) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        return result;
    }

    /// <summary>Removes the prepared entry delta and applies confirmed publication attribution.</summary>
    public CacheCollectionQuotaState Confirm(CacheQuotaChange change, long releasedCredit) =>
        (this with { PreparedEntries = CacheRootQuotaState.Add(this.PreparedEntries, -Math.Max(0, change.Entries)) }).Apply(change, releasedCredit);

    /// <summary>Removes the prepared entry delta and returns space reserved by an unpublished mutation.</summary>
    public CacheCollectionQuotaState AbortPrepared(CacheQuotaChange change, long releasedCredit) =>
        (this with { PreparedEntries = CacheRootQuotaState.Add(this.PreparedEntries, -Math.Max(0, change.Entries)) }).Apply(new(), releasedCredit);
}

/// <summary>Describes signed payload, retirement, metadata and entry-count deltas applied by one confirmed publication.</summary>
public sealed record CacheQuotaChange(long CurrentBytes = 0, long RetiredBytes = 0, long MetadataBytes = 0, long Entries = 0);

/// <summary>Identifies whether a durable space reservation is pending, prepared, published or safely released.</summary>
public enum CacheSpaceCreditState
{
    /// <summary>Space is admitted before physical staging and has no prepared publication delta.</summary>
    Reserved,

    /// <summary>The exact publication delta is durable but its manifest outcome still needs confirmation.</summary>
    Prepared,

    /// <summary>The exact manifest publication and final quota attribution are confirmed.</summary>
    Committed,

    /// <summary>Proven unpublished or deleted physical ownership has returned its reserved space.</summary>
    Released
}

/// <summary>Identifies whether a durable reservation covers payload staging, metadata mutation, a reader pin or index compaction.</summary>
public enum CacheSpaceCreditKind
{
    /// <summary>Space covers staging an immutable payload and its publication metadata.</summary>
    Payload,

    /// <summary>Space covers a metadata mutation without a new payload file.</summary>
    Metadata,

    /// <summary>Space covers a durable reader pin retained until actual reader cleanup.</summary>
    Reader,

    /// <summary>Space covers staged compacted indexes and their predeclared input cleanup plan.</summary>
    Compaction
}

/// <summary>Persists one native operation&apos;s durable space ownership and publication or reclamation evidence.</summary>
/// <example><code>var credit = await ledger.GetCreditAsync(operationId, token); var state = credit.State;</code></example>
public sealed record CacheSpaceCredit
{
    /// <summary>Gets whether new payload admission must leave bounded index-copy and cleanup headroom within both physical quotas.</summary>
    /// <remarks>This protects reclamation capacity without returning any occupied or uncertain credit. Native maintenance and reader pins remain subject to the same hard physical quota.</remarks>
    public bool PreserveMaintenanceHeadroom { get; init; }

    /// <summary>Gets the physical operation category covered by this reservation.</summary>
    public CacheSpaceCreditKind Kind { get; init; }

    /// <summary>Gets the generated unique durable credit ID.</summary>
    public string Id { get; init; }

    /// <summary>Gets the logical collection attribution identifier retained in the serialized credit format.</summary>
    public string CollectionId { get; init; }

    /// <summary>Gets the node identity that may advance this native reservation.</summary>
    public string NodeId { get; init; }

    /// <summary>Gets the predeclared native staging or reader-owner marker name.</summary>
    public string Owner { get; init; }

    /// <summary>Gets the generated payload version, or null for operations without a staged payload.</summary>
    public string Version { get; init; }

    /// <summary>Gets the mutation receipt ID used to reconcile the corresponding shard publication.</summary>
    public string OperationId { get; init; }

    /// <summary>Gets bytes durably admitted for this physical operation before staging begins.</summary>
    public long ReservedBytes { get; init; }

    /// <summary>Gets the creation timestamp in UTC ticks for bounded cleanup ordering.</summary>
    public long CreatedTicks { get; init; }

    /// <summary>Gets the reservation&apos;s durable publication lifecycle state.</summary>
    public CacheSpaceCreditState State { get; init; }

    /// <summary>Gets the full canonical identity used to attribute the operation to a collection and shard.</summary>
    public CacheEntryIdentity Identity { get; init; }

    /// <summary>Gets the signed quota delta predeclared before authoritative shard publication.</summary>
    public CacheQuotaChange PreparedChange { get; init; }

    /// <summary>Gets the exact manifest generation required to confirm a prepared publication.</summary>
    public string PublicationGeneration { get; init; }

    /// <summary>Gets the output segment and manifest generation bound before physical staging.</summary>
    public CacheShardCommitIdentity Commit { get; init; }

    /// <summary>Gets the operation checkpoint coupled to this publication, when tracked invalidation is active.</summary>
    public CacheNativeTrackedProgress TrackedProgress { get; init; }

    /// <summary>Gets whether actual native work termination has been durably established; lease expiry is insufficient.</summary>
    public bool NativeEnded { get; init; }

    /// <summary>Gets the immutable input and output cleanup plan bound to a compaction reservation.</summary>
    public CacheNativeCompactionPlan Compaction { get; init; }

    /// <summary>Gets whether the deleted compaction inputs have already returned their metadata attribution.</summary>
    public bool CompactionReclaimed { get; init; }
}

/// <summary>Binds compaction input files and manifest generation to predeclared output names before staging and later reclamation.</summary>
public sealed record CacheNativeCompactionPlan(string InputGeneration, long InputSequence, long InputBytes, IReadOnlyList<string> Segments,
    string Journal, string OutputJournal);

/// <summary>Persists exact root-ledger checkpoint inputs, outputs and workspace bounds for deterministic control-ledger recovery.</summary>
public sealed record CacheLedgerIntent(string StoreId, string OperationId, string BaseGeneration, CacheShardCommitIdentity Commit,
    string Journal, long CommittedJournalBytes, long WorkspaceBytes, long PreviousOwnedBytes,
    string CheckpointJournal = null, IReadOnlyList<string> RetiredSegments = null);