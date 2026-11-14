// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Describes a bounded metadata page with a fixed operation cutoff.</summary>
/// <param name="Namespace">Application namespace.</param>
/// <param name="Collection">Logical collection across retained contract versions.</param>
/// <param name="Selection">Validated typed selection.</param>
/// <param name="Cutoff">Fixed operation UTC cutoff, retained by continuations.</param>
/// <param name="PageSize">At most 1,000 entries or four MiB of metadata.</param>
/// <param name="Continuation">Opaque provider-issued continuation.</param>
/// <param name="MaximumCandidates">Remaining bounded candidate budget, at most 100,000.</param>
/// <example><code>var page = await provider.QueryAsync(request, cancellationToken);</code></example>
public sealed record CacheMetadataQuery(string Namespace, string Collection, CacheSelection Selection,
    DateTimeOffset Cutoff, int PageSize = 100, string Continuation = null, int MaximumCandidates = 100000);

/// <summary>Reports a bounded unique-version metadata page without claiming an exact total from partial work.</summary>
/// <param name="Entries">Immutable snapshot entries.</param>
/// <param name="Continuation">Opaque continuation or null.</param>
/// <param name="Count">Observed or exact matching count.</param>
/// <param name="CountIsExact">Whether the count covers the complete bounded query.</param>
/// <param name="CandidatesExamined">Candidates charged against the operation budget.</param>
/// <param name="Completed">Whether the indexed walk finished within its explicit bounds.</param>
/// <param name="Reason">A controlled work/candidate-limit reason when more bounded work is needed.</param>
public sealed record CacheMetadataPage(IReadOnlyList<CacheEntryMetadata> Entries, string Continuation,
    long Count, bool CountIsExact, int CandidatesExamined, bool Completed = true, string Reason = null);

/// <summary>Describes an invalidation that establishes fences even when no entry currently matches.</summary>
/// <param name="Namespace">Application namespace.</param>
/// <param name="Collection">Logical collection across contract versions in this store.</param>
/// <param name="Selection">Typed selection; ignored only for explicit clear.</param>
/// <param name="Cutoff">Fixed operation cutoff.</param>
/// <param name="Clear">Whether this is the explicit clear operation.</param>
/// <param name="Continuation">An opaque bounded-work continuation.</param>
/// <param name="OperationId">Optional tracked-operation identity.</param>
public sealed record CacheProviderInvalidation(string Namespace, string Collection, CacheSelection Selection,
    DateTimeOffset Cutoff, bool Clear = false, string Continuation = null, string OperationId = null);

/// <summary>Conditions exact removal on optional captured source authority without opening payload content.</summary>
/// <param name="Identity">The captured exact contract identity.</param>
/// <param name="Condition">Optional acquired expected version. Create-only removal is invalid.</param>
/// <param name="Fence">Source authority captured before a cache-managed read, when applicable.</param>
/// <param name="Owner">Confirmed shared-store owner, when applicable.</param>
/// <param name="Deadline">The original source deadline, checked at the atomic mutation boundary.</param>
/// <param name="OnlyExpectedVersion">Whether to remove only the expected contract version, as for an unretained absence refresh.</param>
/// <example><code>var request = new CacheProviderRemoval(identity, new(version), fence, Deadline: deadline, OnlyExpectedVersion: true);</code></example>
public sealed record CacheProviderRemoval(CacheEntryIdentity Identity, CacheWriteCondition Condition = default,
    CachePublicationFence? Fence = null, CacheOwnerToken? Owner = null, DateTimeOffset? Deadline = null, bool OnlyExpectedVersion = false);

/// <summary>Describes confirmed source ownership acquisition with a fixed total-work deadline.</summary>
/// <param name="Identity">The exact source identity.</param>
/// <param name="NodeId">Bounded process identity, never a user/tenant label.</param>
/// <param name="InterestId">One bounded local observation group's interest.</param>
/// <param name="Deadline">Fixed total source deadline; renewal cannot extend it.</param>
/// <param name="LeaseLifetime">The bounded renewal lease.</param>
/// <param name="Independent">Whether to register distinct admitted work instead of joining an existing identity flight.</param>
public sealed record CacheOwnershipRequest(CacheEntryIdentity Identity, string NodeId, string InterestId,
    DateTimeOffset Deadline, TimeSpan LeaseLifetime, bool Independent = false);

/// <summary>Reports authoritative acquisition or observation of one shared source flight.</summary>
/// <param name="State">The observed ownership/completion state.</param>
/// <param name="Owner">Issued only for a confirmed owner.</param>
/// <param name="Fence">The fence captured before source access.</param>
/// <param name="FlightId">Opaque identity distinguishing consecutive flights.</param>
/// <param name="LeaseExpiresAt">Authoritative bounded lease expiry.</param>
/// <param name="Version">Confirmed published version, where applicable.</param>
/// <param name="Failure">Controlled completion failure without source exception details.</param>
/// <param name="IsNotFound">Whether the repeatable source explicitly confirmed metadata-only absence.</param>
public sealed record CacheOwnershipObservation(CacheOwnershipState State, CacheOwnerToken? Owner,
    CachePublicationFence Fence, string FlightId, DateTimeOffset LeaseExpiresAt,
    CacheEntryVersion? Version = null, CacheErrorCode? Failure = null, bool IsNotFound = false);

/// <summary>Identifies shared ownership and terminal completion states.</summary>
public enum CacheOwnershipState
{
    /// <summary>This request obtained confirmed source authority.</summary>
    Owned,

    /// <summary>Another owner is active; observe without invoking a source.</summary>
    Pending,

    /// <summary>A version was confirmed published.</summary>
    Published,

    /// <summary>Owned work ended without a newly published source result; no typed value or cache-recheck result is transported between nodes.</summary>
    Unretained,

    /// <summary>The owned source failed. Followers receive that failure without automatic takeover.</summary>
    Failed,

    /// <summary>Authority ended or was revoked. Late work must not publish.</summary>
    Lost
}

/// <summary>Records one terminal shared source completion.</summary>
/// <param name="State">Published, Unretained, Failed, or Lost.</param>
/// <param name="Version">Confirmed publication token only.</param>
/// <param name="Failure">Controlled terminal failure classification.</param>
/// <param name="IsNotFound">Whether an unretained or retained absence can be delivered without transporting a value.</param>
public sealed record CacheOwnershipCompletion(CacheOwnershipState State, CacheEntryVersion? Version = null, CacheErrorCode? Failure = null,
    bool IsNotFound = false);

/// <summary>Reports provider-owned storage accounting, including staging and pinned obsolete versions.</summary>
/// <param name="CurrentBytes">Exact current retained payload/envelope bytes.</param>
/// <param name="RetiredBytes">Obsolete generations still protected by readers/recovery.</param>
/// <param name="StagingBytes">Reserved or actual unpublished bytes.</param>
/// <param name="MetadataBytes">Exact or conservatively charged metadata/index bytes.</param>
/// <param name="CurrentEntries">Current entry count.</param>
/// <param name="MaximumBytes">Configured provider storage cap.</param>
/// <param name="IsExact">Whether every byte category is exact.</param>
public sealed record CacheProviderQuota(long CurrentBytes, long RetiredBytes, long StagingBytes,
    long MetadataBytes, long CurrentEntries, long MaximumBytes, bool IsExact);

/// <summary>Bounds one maintenance pass. No request or worker applies schema DDL.</summary>
/// <param name="BatchSize">Maximum entries reclaimed.</param>
/// <param name="RetentionCutoff">Physical retention grace cutoff.</param>
/// <param name="NewWorkDeadline">Deadline for admitting more cleanup work.</param>
public sealed record CacheMaintenanceRequest(int BatchSize, DateTimeOffset RetentionCutoff, DateTimeOffset NewWorkDeadline);

/// <summary>Reports cleanup progress and retained unresolved work.</summary>
/// <param name="ReclaimedEntries">Completed physical reclamations.</param>
/// <param name="ReclaimedBytes">Completed released byte charges.</param>
/// <param name="HasMore">Whether another bounded pass may make progress.</param>
/// <param name="UncertainOperations">Operations whose quota must remain reserved.</param>
public sealed record CacheMaintenanceOutcome(long ReclaimedEntries, long ReclaimedBytes, bool HasMore, long UncertainOperations);

/// <summary>Stores bounded provider-owned invalidation operation state and continuation.</summary>
/// <example><code>var operation = await provider.GetOperationAsync(id, cancellationToken);</code></example>
public sealed record CacheProviderOperation
{
    /// <summary>Gets the stable operation identity.</summary>
    public string Id { get; init; }

    /// <summary>Gets the target logical collection.</summary>
    public string Collection { get; init; }

    /// <summary>Gets the fixed selection cutoff.</summary>
    public DateTimeOffset Cutoff { get; init; }

    /// <summary>Gets the selected operation.</summary>
    public CacheProviderInvalidation Request { get; init; }

    /// <summary>Gets current provider-owned execution state.</summary>
    public CacheOperationState State { get; init; }

    /// <summary>Gets whether cancellation should stop remaining work at the next confirmed safe boundary.</summary>
    public bool CancellationRequested { get; init; }

    /// <summary>Gets the controlled last failure classification, without a source exception or sensitive provider detail.</summary>
    public CacheErrorCode? FailureCode { get; init; }

    /// <summary>Gets a concurrency token for state transitions.</summary>
    public string Revision { get; init; }

    /// <summary>Gets the last confirmed aggregate outcome.</summary>
    public CacheInvalidationOutcome Outcome { get; init; }

    /// <summary>Gets the creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the last confirmed transition timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets a bounded adapter/caller origin.</summary>
    public string Origin { get; init; }
}

/// <summary>Identifies tracked operation execution states.</summary>
public enum CacheOperationState
{
    /// <summary>Waiting for provider-owned maintenance execution.</summary>
    Pending,

    /// <summary>Compatibility alias for pending work.</summary>
    Queued = Pending,

    /// <summary>Executing a bounded batch.</summary>
    Running,

    /// <summary>Paused between batches.</summary>
    Paused,

    /// <summary>Cancelled with already completed effects retained.</summary>
    Cancelled,

    /// <summary>All selected work completed.</summary>
    Completed,

    /// <summary>Stopped with an explicit partial failure.</summary>
    Failed
}