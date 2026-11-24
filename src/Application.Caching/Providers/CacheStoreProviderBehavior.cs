// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Forwards the complete cache provider contract for focused, once-composed optional behaviors.</summary>
/// <param name="inner">The one complete authoritative provider.</param>
/// <example><code>public sealed class ProviderLogging(ICacheStoreProvider inner) : CacheStoreProviderBehavior(inner);</code></example>
public abstract class CacheStoreProviderBehavior(ICacheStoreProvider inner) : ICacheStoreProvider
{
    /// <summary>Gets the complete authoritative provider wrapped by this optional behavior.</summary>
    protected ICacheStoreProvider Inner { get; } = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc />
    public string Name => this.Inner.Name;

    /// <inheritdoc />
    public CacheProviderCapabilities Capabilities => this.Inner.Capabilities;

    /// <inheritdoc />
    public virtual ValueTask<Result> InitializeAsync(IReadOnlyList<CacheProviderCollection> collections, CancellationToken cancellationToken = default) =>
        this.Inner.InitializeAsync(collections, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheReadValidation>> LookupAsync(CacheEntryIdentity identity, CacheReadRequest request, CancellationToken cancellationToken = default) =>
        this.Inner.LookupAsync(identity, request, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheReadValidation>> ValidateReadAsync(CacheEntryIdentity identity, CacheEntryVersion version, CacheReadRequest request,
        CancellationToken cancellationToken = default) =>
        this.Inner.ValidateReadAsync(identity, version, request, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CachePublicationFence>> CaptureFenceAsync(CacheEntryIdentity identity, CancellationToken cancellationToken = default) =>
        this.Inner.CaptureFenceAsync(identity, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result> ReleaseFenceAsync(CacheEntryIdentity identity, CachePublicationFence fence, CancellationToken cancellationToken = default) =>
        this.Inner.ReleaseFenceAsync(identity, fence, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<ICacheProviderWriteSession>> BeginWriteAsync(CacheProviderWriteRequest request, CancellationToken cancellationToken = default) =>
        this.Inner.BeginWriteAsync(request, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheEntryMetadata>> TouchAsync(CacheEntryIdentity identity, CacheEntryVersion? version = null, CancellationToken cancellationToken = default) =>
        this.Inner.TouchAsync(identity, version, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheEntryIdentity identity, CacheWriteCondition condition = default,
        CancellationToken cancellationToken = default) =>
        this.Inner.RemoveAsync(identity, condition, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheProviderRemoval request, CancellationToken cancellationToken = default) =>
        this.Inner.RemoveAsync(request, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheMetadataPage>> QueryAsync(CacheMetadataQuery query, CancellationToken cancellationToken = default) =>
        this.Inner.QueryAsync(query, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheRefreshPage>> QueryRefreshAsync(CacheRefreshQuery query, CancellationToken cancellationToken = default) =>
        this.Inner.QueryRefreshAsync(query, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result> RecordRefreshAttemptAsync(CacheRefreshAttempt attempt, CancellationToken cancellationToken = default) =>
        this.Inner.RecordRefreshAttemptAsync(attempt, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheProviderInvalidation request, CancellationToken cancellationToken = default) =>
        this.Inner.InvalidateAsync(request, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheOwnershipObservation>> AcquireOwnershipAsync(CacheOwnershipRequest request, CancellationToken cancellationToken = default) =>
        this.Inner.AcquireOwnershipAsync(request, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheOwnershipObservation>> RenewOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CancellationToken cancellationToken = default) =>
        this.Inner.RenewOwnershipAsync(identity, owner, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheOwnershipObservation>> ObserveOwnershipAsync(CacheOwnershipRequest request, string flightId, CancellationToken cancellationToken = default) =>
        this.Inner.ObserveOwnershipAsync(request, flightId, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result> CompleteOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CacheOwnershipCompletion completion,
        CancellationToken cancellationToken = default) =>
        this.Inner.CompleteOwnershipAsync(identity, owner, completion, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result> ReleaseInterestAsync(CacheEntryIdentity identity, string flightId, string interestId, CancellationToken cancellationToken = default) =>
        this.Inner.ReleaseInterestAsync(identity, flightId, interestId, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheMaintenanceOutcome>> MaintainAsync(CacheMaintenanceRequest request, CancellationToken cancellationToken = default) =>
        this.Inner.MaintainAsync(request, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheProviderQuota>> GetQuotaAsync(CancellationToken cancellationToken = default) =>
        this.Inner.GetQuotaAsync(cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheWriteOutcome>> ReconcileAsync(string operationId, CancellationToken cancellationToken = default) =>
        this.Inner.ReconcileAsync(operationId, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheProviderOperation>> SaveOperationAsync(CacheProviderOperation operation, string expectedRevision = null,
        CancellationToken cancellationToken = default) =>
        this.Inner.SaveOperationAsync(operation, expectedRevision, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<CacheProviderOperation>> GetOperationAsync(string operationId, CancellationToken cancellationToken = default) =>
        this.Inner.GetOperationAsync(operationId, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask<Result<IReadOnlyList<CacheProviderOperation>>> GetOperationsAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        this.Inner.GetOperationsAsync(limit, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask DisposeAsync() => this.Inner.DisposeAsync();
}