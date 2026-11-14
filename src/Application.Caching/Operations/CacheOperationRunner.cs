// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Runs one provider-owned invalidation continuation unit without looping around its query or work budget.</summary>
/// <param name="access">The shared provider admission and accounting boundary.</param>
/// <param name="scopeFactory">The factory for one owned async scope per admitted continuation unit.</param>
/// <param name="diagnostics">Optional preallocated observations for actual attempted provider units.</param>
/// <example><code>var status = await runner.RunUnitAsync(binding, operationId, cancellationToken);</code></example>
public sealed class CacheOperationRunner(CacheWorkerAccess access, IServiceScopeFactory scopeFactory, CacheDiagnostics diagnostics = null)
{
    /// <summary>Claims a retained operation and executes at most one conditional provider batch.</summary>
    /// <param name="binding">The currently authorized, ready collection binding.</param>
    /// <param name="operationId">The retained provider operation identity.</param>
    /// <param name="cancellationToken">Cancellation before native admission; uncertain admitted work remains provider-owned.</param>
    /// <returns>The provider's atomically committed checkpoint/status, or an explicit unavailable/conflict failure.</returns>
    public async ValueTask<Result<CacheProviderOperation>> RunUnitAsync(CacheCollectionBinding binding, string operationId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var ready = await access.EnsureReadyAsync(binding, scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        if (ready.IsFailure) { return Result<CacheProviderOperation>.Failure(ready); }

        var found = await access.CallAsync(binding, (provider, token) => provider.GetOperationAsync(operationId, token), cancellationToken).ConfigureAwait(false);
        if (found.IsFailure) { return found; }

        var operation = found.Value;
        if (operation.State is CacheOperationState.Completed or CacheOperationState.Cancelled or CacheOperationState.Failed or CacheOperationState.Paused)
        {
            return found;
        }

        if (operation.CancellationRequested)
        {
            return await access.CallAsync(binding, (provider, token) => provider.SaveOperationAsync(operation with
            {
                State = CacheOperationState.Cancelled
            }, operation.Revision, token), cancellationToken).ConfigureAwait(false);
        }

        var claimed = await access.CallAsync(binding, (provider, token) => provider.SaveOperationAsync(operation with
        {
            State = CacheOperationState.Running
        }, operation.Revision, token), cancellationToken).ConfigureAwait(false);
        if (claimed.IsFailure) { return claimed; }

        var request = claimed.Value.Request with { OperationId = operationId, Continuation = claimed.Value.Outcome?.Continuation };
        var result = await access.CallAsync(binding, (provider, token) => provider.InvalidateAsync(request, token), cancellationToken).ConfigureAwait(false);
        var observations = diagnostics?.ForCollection(binding.Name);
        if (result.IsSuccess)
        {
            observations?.Record(CacheObservationKind.InvalidationCompleted);
            observations?.Record(CacheObservationKind.InvalidatedEntries,
                Math.Max(0, result.Value.AffectedCount - (claimed.Value.Outcome?.AffectedCount ?? 0)));
        }
        else { observations?.Record(CacheObservationKind.InvalidationFailed); }

        var observed = await access.CallAsync(binding, (provider, token) => provider.GetOperationAsync(operationId, token), cancellationToken).ConfigureAwait(false);
        if (observed.IsFailure) { return observed; }

        if (result.IsSuccess) { return observed; }

        // A native timeout does not prove the unit ended. Keep Running and reconcile its provider checkpoint on a later pass.
        if (result.Errors.OfType<CacheError>().Any(e => e.Code is CacheErrorCode.Indeterminate or CacheErrorCode.ProviderUnavailable))
        {
            return Result<CacheProviderOperation>.Failure(result);
        }

        return await access.CallAsync(binding, (provider, token) => provider.SaveOperationAsync(observed.Value with
        {
            State = observed.Value.CancellationRequested ? CacheOperationState.Cancelled : CacheOperationState.Failed,
            FailureCode = result.Errors.OfType<CacheError>().FirstOrDefault()?.Code ?? CacheErrorCode.Contract
        }, observed.Value.Revision, token), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Continues a bounded number of retained active records during the ordinary maintenance flow.</summary>
    /// <param name="binding">The representative ready collection for the selected provider.</param>
    /// <param name="catalog">The current immutable catalog, used to retain explicit per-collection scope.</param>
    /// <param name="maximumUnits">The maximum provider continuation units to admit.</param>
    /// <param name="deadline">The fixed deadline for admitting more units.</param>
    /// <param name="timeProvider">The worker clock.</param>
    /// <param name="cancellationToken">The worker shutdown token.</param>
    /// <returns>The number of confirmed unit attempts.</returns>
    public async ValueTask<int> RunProviderAsync(CacheCollectionBinding binding, CacheCatalog catalog, int maximumUnits,
        DateTimeOffset deadline, TimeProvider timeProvider, CancellationToken cancellationToken = default)
    {
        var records = await access.CallAsync(binding, (provider, token) => provider.GetOperationsAsync(Math.Min(1000, maximumUnits), token), cancellationToken).ConfigureAwait(false);
        if (records.IsFailure) { return 0; }

        var completed = 0;
        foreach (var operation in records.Value.Where(o => o.State is CacheOperationState.Pending or CacheOperationState.Running))
        {
            if (completed >= maximumUnits || timeProvider.GetUtcNow() >= deadline) { break; }

            if (!catalog.Collections.TryGetValue(operation.Collection, out var target) || !target.Options.Enabled || target.Provider.Name != binding.Provider.Name ||
                operation.Request.Namespace != CacheIdentityNormalizer.Namespace(catalog.Options.Namespace)) { continue; }

            await this.RunUnitAsync(target, operation.Id, cancellationToken).ConfigureAwait(false);
            completed++;
        }

        return completed;
    }
}