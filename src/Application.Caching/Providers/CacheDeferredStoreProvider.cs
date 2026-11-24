// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defers a complete native provider until enabled cache work requests it, preserving one registry-owned lifetime.</summary>
/// <param name="binding">The declared named provider binding.</param>
/// <param name="resolve">Resolves the registry-owned provider without creating dynamic registrations.</param>
/// <remarks>Constructing disabled collection handles performs no native factory or provider calls.</remarks>
/// <example><code>var deferred = new CacheDeferredStoreProvider(binding, () =&gt; registry.Get(binding.Name));</code></example>
public sealed class CacheDeferredStoreProvider(CacheProviderBinding binding, Func<ICacheStoreProvider> resolve) : ICacheStoreProvider, ICacheProviderInfrastructureObserver
{
    private ICacheStoreProvider resolved;

    /// <inheritdoc />
    public string Name => binding.Name;

    /// <inheritdoc />
    public CacheProviderCapabilities Capabilities => binding.Capabilities;

    /// <inheritdoc />
    public long CircuitEpoch => (this.resolved as ICacheProviderInfrastructureObserver)?.CircuitEpoch ?? 0;

    /// <inheritdoc />
    public ValueTask ReportInfrastructureTimeoutAsync(long epoch) => this.resolved is ICacheProviderInfrastructureObserver observer ?
        observer.ReportInfrastructureTimeoutAsync(epoch) : ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask<Result> InitializeAsync(IReadOnlyList<CacheProviderCollection> collections, CancellationToken cancellationToken = default) =>
        this.Resolve().InitializeAsync(collections, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheReadValidation>> LookupAsync(CacheEntryIdentity identity, CacheReadRequest request, CancellationToken cancellationToken = default) =>
        this.Resolve().LookupAsync(identity, request, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheReadValidation>> ValidateReadAsync(CacheEntryIdentity identity, CacheEntryVersion version, CacheReadRequest request,
        CancellationToken cancellationToken = default) => this.Resolve().ValidateReadAsync(identity, version, request, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CachePublicationFence>> CaptureFenceAsync(CacheEntryIdentity identity, CancellationToken cancellationToken = default) =>
        this.Resolve().CaptureFenceAsync(identity, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> ReleaseFenceAsync(CacheEntryIdentity identity, CachePublicationFence fence, CancellationToken cancellationToken = default) =>
        this.Resolve().ReleaseFenceAsync(identity, fence, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<ICacheProviderWriteSession>> BeginWriteAsync(CacheProviderWriteRequest request, CancellationToken cancellationToken = default) =>
        this.Resolve().BeginWriteAsync(request, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheEntryMetadata>> TouchAsync(CacheEntryIdentity identity, CacheEntryVersion? version = null, CancellationToken cancellationToken = default) =>
        this.Resolve().TouchAsync(identity, version, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheEntryIdentity identity, CacheWriteCondition condition = default,
        CancellationToken cancellationToken = default) => this.Resolve().RemoveAsync(identity, condition, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheProviderRemoval request, CancellationToken cancellationToken = default) =>
        this.Resolve().RemoveAsync(request, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheMetadataPage>> QueryAsync(CacheMetadataQuery query, CancellationToken cancellationToken = default) =>
        this.Resolve().QueryAsync(query, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheRefreshPage>> QueryRefreshAsync(CacheRefreshQuery query, CancellationToken cancellationToken = default) =>
        this.Resolve().QueryRefreshAsync(query, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> RecordRefreshAttemptAsync(CacheRefreshAttempt attempt, CancellationToken cancellationToken = default) =>
        this.Resolve().RecordRefreshAttemptAsync(attempt, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheProviderInvalidation request, CancellationToken cancellationToken = default) =>
        this.Resolve().InvalidateAsync(request, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheOwnershipObservation>> AcquireOwnershipAsync(CacheOwnershipRequest request, CancellationToken cancellationToken = default) =>
        this.Resolve().AcquireOwnershipAsync(request, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheOwnershipObservation>> RenewOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CancellationToken cancellationToken = default) =>
        this.Resolve().RenewOwnershipAsync(identity, owner, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheOwnershipObservation>> ObserveOwnershipAsync(CacheOwnershipRequest request, string flightId, CancellationToken cancellationToken = default) =>
        this.Resolve().ObserveOwnershipAsync(request, flightId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> CompleteOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CacheOwnershipCompletion completion,
        CancellationToken cancellationToken = default) => this.Resolve().CompleteOwnershipAsync(identity, owner, completion, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> ReleaseInterestAsync(CacheEntryIdentity identity, string flightId, string interestId, CancellationToken cancellationToken = default) =>
        this.Resolve().ReleaseInterestAsync(identity, flightId, interestId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheMaintenanceOutcome>> MaintainAsync(CacheMaintenanceRequest request, CancellationToken cancellationToken = default) =>
        this.Resolve().MaintainAsync(request, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheProviderQuota>> GetQuotaAsync(CancellationToken cancellationToken = default) => this.Resolve().GetQuotaAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheWriteOutcome>> ReconcileAsync(string operationId, CancellationToken cancellationToken = default) =>
        this.Resolve().ReconcileAsync(operationId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheProviderOperation>> SaveOperationAsync(CacheProviderOperation operation, string expectedRevision = null,
        CancellationToken cancellationToken = default) => this.Resolve().SaveOperationAsync(operation, expectedRevision, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheProviderOperation>> GetOperationAsync(string operationId, CancellationToken cancellationToken = default) =>
        this.Resolve().GetOperationAsync(operationId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<CacheProviderOperation>>> GetOperationsAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        this.Resolve().GetOperationsAsync(limit, cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask ReportInfrastructureOutcomeAsync(long epoch, bool infrastructureFailure) => this.resolved is ICacheProviderInfrastructureObserver observer ?
        observer.ReportInfrastructureOutcomeAsync(epoch, infrastructureFailure) : ValueTask.CompletedTask;

    private ICacheStoreProvider Resolve() => this.resolved ??= resolve();
}