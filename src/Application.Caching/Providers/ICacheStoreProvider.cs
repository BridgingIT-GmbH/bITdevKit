// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Owns one complete authoritative cache store, including payload, indexes, fences, quota, readers, and recovery.</summary>
/// <remarks>No operation may substitute an unbounded payload scan for an indexed metadata query. Cancellation does not prove native I/O stopped.</remarks>
/// <example><code>var result = await provider.LookupAsync(identity, new(OpenPayload: true), cancellationToken);</code></example>
public interface ICacheStoreProvider : IAsyncDisposable
{
    /// <summary>Gets the registered stable provider name.</summary>
    string Name { get; }

    /// <summary>Gets truthful native store capabilities.</summary>
    CacheProviderCapabilities Capabilities { get; }

    /// <summary>Checks readiness, format/catalog compatibility, and required deployed schema without applying DDL.</summary>
    ValueTask<Result> InitializeAsync(IReadOnlyList<CacheProviderCollection> collections, CancellationToken cancellationToken = default);

    /// <summary>Acquires one indexed validity/fence/optional payload snapshot. Negative entries need no stream.</summary>
    ValueTask<Result<CacheReadValidation>> LookupAsync(CacheEntryIdentity identity, CacheReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Establishes authoritative validity on every hot reuse without reading payload content unless explicitly requested.</summary>
    ValueTask<Result<CacheReadValidation>> ValidateReadAsync(CacheEntryIdentity identity, CacheEntryVersion expectedVersion,
        CacheReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Captures publication authority before engine-managed source access or direct staging.</summary>
    ValueTask<Result<CachePublicationFence>> CaptureFenceAsync(CacheEntryIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>Releases bounded captured source/staging authority after the actual work ends. Repeated release is harmless.</summary>
    ValueTask<Result> ReleaseFenceAsync(CacheEntryIdentity identity, CachePublicationFence fence, CancellationToken cancellationToken = default);

    /// <summary>Starts bounded same-store staging without changing the current visible version.</summary>
    ValueTask<Result<ICacheProviderWriteSession>> BeginWriteAsync(CacheProviderWriteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Atomically renews authoritative sliding expiry, bounded by its persisted absolute cap.</summary>
    ValueTask<Result<CacheEntryMetadata>> TouchAsync(CacheEntryIdentity identity, CacheEntryVersion? expectedVersion = null, CancellationToken cancellationToken = default);

    /// <summary>Fences the key even when absent, then conditionally removes its current version. A conflict changes nothing.</summary>
    ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheEntryIdentity identity, CacheWriteCondition condition = default, CancellationToken cancellationToken = default);

    /// <summary>Atomically verifies captured source authority and removes an expected version without retaining an absence marker.</summary>
    ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheProviderRemoval request, CancellationToken cancellationToken = default);

    /// <summary>Returns a bounded payload-free indexed page across retained contract versions.</summary>
    ValueTask<Result<CacheMetadataPage>> QueryAsync(CacheMetadataQuery query, CancellationToken cancellationToken = default);

    /// <summary>Queries bounded indexed refresh metadata for the host's current contract without payload access or foreground activity.</summary>
    ValueTask<Result<CacheRefreshPage>> QueryRefreshAsync(CacheRefreshQuery query, CancellationToken cancellationToken = default);

    /// <summary>Conditionally records finite scheduler backoff against the same current version; obsolete attempts are harmless.</summary>
    ValueTask<Result> RecordRefreshAttemptAsync(CacheRefreshAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>Establishes invalidation fences before bounded version-conditional deletion. Empty selection matches nothing.</summary>
    ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheProviderInvalidation request, CancellationToken cancellationToken = default);

    /// <summary>Attempts authoritative shared-store ownership. Unavailable or uncertain authority fails closed.</summary>
    ValueTask<Result<CacheOwnershipObservation>> AcquireOwnershipAsync(CacheOwnershipRequest request, CancellationToken cancellationToken = default);

    /// <summary>Renews confirmed ownership only within the fixed deadline and while interested callers remain.</summary>
    ValueTask<Result<CacheOwnershipObservation>> RenewOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CancellationToken cancellationToken = default);

    /// <summary>Registers/renews bounded remote interest and observes one flight. It never invokes source work.</summary>
    ValueTask<Result<CacheOwnershipObservation>> ObserveOwnershipAsync(CacheOwnershipRequest request, string flightId, CancellationToken cancellationToken = default);

    /// <summary>Completes a confirmed owned flight atomically; no uncached typed value crosses nodes.</summary>
    ValueTask<Result> CompleteOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CacheOwnershipCompletion completion, CancellationToken cancellationToken = default);

    /// <summary>Withdraws one local observation group's interest without cancelling other interested callers.</summary>
    ValueTask<Result> ReleaseInterestAsync(CacheEntryIdentity identity, string flightId, string interestId, CancellationToken cancellationToken = default);

    /// <summary>Performs bounded native cleanup/recovery and retains unresolved charges.</summary>
    ValueTask<Result<CacheMaintenanceOutcome>> MaintainAsync(CacheMaintenanceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns storage/quota accounting without traversing payloads.</summary>
    ValueTask<Result<CacheProviderQuota>> GetQuotaAsync(CancellationToken cancellationToken = default);

    /// <summary>Reconciles an uncertain write using its stable provider operation identity.</summary>
    ValueTask<Result<CacheWriteOutcome>> ReconcileAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>Creates or conditionally transitions bounded provider-owned operation state.</summary>
    ValueTask<Result<CacheProviderOperation>> SaveOperationAsync(CacheProviderOperation operation, string expectedRevision = null, CancellationToken cancellationToken = default);

    /// <summary>Reads provider-owned operation state; missing state is an explicit NotFound failure.</summary>
    ValueTask<Result<CacheProviderOperation>> GetOperationAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>Returns a bounded page of tracked operation records.</summary>
    ValueTask<Result<IReadOnlyList<CacheProviderOperation>>> GetOperationsAsync(int limit = 100, CancellationToken cancellationToken = default);
}