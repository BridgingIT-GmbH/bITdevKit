// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Submits and controls bounded provider-owned invalidation records for currently selected collection scopes.</summary>
/// <param name="catalog">The immutable startup catalog defining allowed targets.</param>
/// <param name="access">The shared provider readiness and native accounting boundary.</param>
/// <param name="runner">The single-unit cache-specific continuation runner.</param>
/// <param name="scopeFactory">The factory for owned readiness/control scopes.</param>
/// <param name="timeProvider">The clock used to capture immutable operation cutoffs.</param>
/// <param name="diagnostics">Optional preallocated collection observations for validated tracked requests.</param>
/// <remarks>Management adapters apply current authorization before each call. No request principal is retained in provider metadata.</remarks>
/// <example><code>var accepted = await operations.SubmitAsync(new(["cards"], selection, Background: true), cancellationToken);</code></example>
public sealed class CacheOperationService(CacheCatalog catalog, CacheWorkerAccess access, CacheOperationRunner runner,
    IServiceScopeFactory scopeFactory, TimeProvider timeProvider, CacheDiagnostics diagnostics = null)
{
    /// <summary>Reserves one child status per target before mutation, then optionally runs one bounded synchronous unit.</summary>
    /// <param name="request">The explicit bounded scope, filter/clear kind, and operational metadata.</param>
    /// <param name="cancellationToken">The caller's admission/wait cancellation token.</param>
    /// <returns>Ordered independent child results. Acceptance never claims completed invalidation.</returns>
    public async ValueTask<Result<IReadOnlyList<Result<CacheOperationSubmission>>>> SubmitAsync(CacheInvalidationOperation request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Collections is null || request.Collections.Count is <= 0 or > 32 || request.Origin?.Length > 128)
        {
            return Result<IReadOnlyList<Result<CacheOperationSubmission>>>.Failure(new CacheError(CacheErrorCode.Validation, "Invalid operation target or metadata bounds."));
        }

        var names = request.Collections.Select(CacheIdentityNormalizer.Name).ToArray();
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length || names.Any(n => !catalog.Collections.ContainsKey(n)))
        {
            return Result<IReadOnlyList<Result<CacheOperationSubmission>>>.Failure(new CacheError(CacheErrorCode.Validation, "Operation targets must be distinct declared collections."));
        }

        var cutoff = timeProvider.GetUtcNow();
        var results = new List<Result<CacheOperationSubmission>>(names.Length);
        foreach (var name in names)
        {
            var binding = catalog.Collections[name];
            if (!catalog.Options.Enabled || !binding.Options.Enabled)
            {
                results.Add(Result<CacheOperationSubmission>.Failure(new CacheError(CacheErrorCode.Unsupported, "Disabled collections admit no background operation.")));
                continue;
            }

            var validation = CacheSelectionValidator.Validate(request.Clear ? CacheSelection.None : request.Selection, binding.Options.Dimensions);
            if (validation.IsFailure) { results.Add(Result<CacheOperationSubmission>.Failure(validation)); continue; }

            diagnostics?.ForCollection(name).Record(CacheObservationKind.InvalidationRequested);
            await using var scope = scopeFactory.CreateAsyncScope();
            var ready = await access.EnsureReadyAsync(binding, scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
            if (ready.IsFailure) { results.Add(Result<CacheOperationSubmission>.Failure(ready)); continue; }

            var id = Guid.NewGuid().ToString("N");
            var operation = new CacheProviderOperation
            {
                Id = id, Collection = name, Cutoff = cutoff, State = CacheOperationState.Pending,
                Request = new(catalog.Options.Namespace, name, request.Selection ?? CacheSelection.None, cutoff, request.Clear, OperationId: id),
                Origin = request.Origin
            };
            var saved = await access.CallAsync(binding, (provider, token) => provider.SaveOperationAsync(operation, cancellationToken: token), cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure) { results.Add(Result<CacheOperationSubmission>.Failure(saved)); continue; }

            var status = request.Background ? saved : await runner.RunUnitAsync(binding, id, cancellationToken).ConfigureAwait(false);
            results.Add(status.IsSuccess ? Result<CacheOperationSubmission>.Success(new(new(binding.Provider.Name, name, id), status.Value, request.Background)) :
                Result<CacheOperationSubmission>.Failure(status));
        }

        return Result<IReadOnlyList<Result<CacheOperationSubmission>>>.Success(results.AsReadOnly());
    }

    /// <summary>Reads current retained status; unknown or expired handles remain unavailable.</summary>
    /// <param name="handle">The explicitly selected provider/collection child handle.</param>
    /// <param name="cancellationToken">The caller's readiness/read cancellation token.</param>
    /// <returns>The current status or a controlled unavailable/scope failure.</returns>
    public ValueTask<Result<CacheProviderOperation>> GetStatusAsync(CacheOperationHandle handle, CancellationToken cancellationToken = default) =>
        this.ControlAsync(handle, null, cancellationToken);

    /// <summary>Idempotently resumes the retained remainder of cancelled or failed work without changing its captured scope/cutoff.</summary>
    /// <param name="handle">The currently authorized child handle.</param>
    /// <param name="cancellationToken">The caller's readiness/control cancellation token.</param>
    /// <returns>The retained pending/completed status, never an implicit new operation.</returns>
    public ValueTask<Result<CacheProviderOperation>> ResumeAsync(CacheOperationHandle handle, CancellationToken cancellationToken = default) =>
        this.ControlAsync(handle, false, cancellationToken);

    /// <summary>Idempotently requests cancellation at a confirmed safe unit boundary without rolling back committed effects.</summary>
    /// <param name="handle">The currently authorized child handle.</param>
    /// <param name="cancellationToken">The caller's readiness/control cancellation token.</param>
    /// <returns>The confirmed status, including its cancellation-request flag while admitted work remains running.</returns>
    public ValueTask<Result<CacheProviderOperation>> CancelAsync(CacheOperationHandle handle, CancellationToken cancellationToken = default) =>
        this.ControlAsync(handle, true, cancellationToken);

    private async ValueTask<Result<CacheProviderOperation>> ControlAsync(CacheOperationHandle handle, bool? cancel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!catalog.Collections.TryGetValue(CacheIdentityNormalizer.Name(handle.Collection), out var binding) || binding.Provider.Name != CacheIdentityNormalizer.Name(handle.Provider) ||
            !catalog.Options.Enabled || !binding.Options.Enabled)
        {
            return Result<CacheProviderOperation>.Failure(new CacheError(CacheErrorCode.Validation, "The handle is outside the selected enabled collection scope."));
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var ready = await access.EnsureReadyAsync(binding, scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        if (ready.IsFailure) { return Result<CacheProviderOperation>.Failure(ready); }

        var found = await access.CallAsync(binding, (provider, token) => provider.GetOperationAsync(handle.Id, token), cancellationToken).ConfigureAwait(false);
        if (found.IsFailure) { return found; }

        if (found.Value.Request.Namespace != CacheIdentityNormalizer.Namespace(catalog.Options.Namespace) || found.Value.Collection != binding.Name)
        {
            return Result<CacheProviderOperation>.Failure(new CacheError(CacheErrorCode.NotFound, "The retained operation is unavailable in this selected scope."));
        }

        var current = found.Value;
        if (!cancel.HasValue || current.State == CacheOperationState.Completed || (cancel.Value && current.State == CacheOperationState.Cancelled) ||
            (!cancel.Value && current.State is CacheOperationState.Pending or CacheOperationState.Running)) { return found; }

        var changed = cancel.Value ? current with
        {
            CancellationRequested = true,
            State = current.State == CacheOperationState.Running ? CacheOperationState.Running : CacheOperationState.Cancelled
        } : current with { CancellationRequested = false, State = CacheOperationState.Pending, FailureCode = null };
        return await access.CallAsync(binding, (provider, token) => provider.SaveOperationAsync(changed, current.Revision, token), cancellationToken).ConfigureAwait(false);
    }
}