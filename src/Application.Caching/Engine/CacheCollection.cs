// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Implements declared typed cache operations with bounded provider access, source coordination and confirmed publication.</summary>
/// <typeparam name="T">The declared value or operation result type.</typeparam>
/// <example><code>var cards = cache.GetCollection&lt;ProductCard&gt;(&quot;cards&quot;); var result = await cards.GetAsync(&quot;sku:42&quot;);</code></example>
public sealed class CacheCollection<T> : ICacheCollection<T>
{
    private readonly CacheCollectionRuntime runtime;
    private readonly CacheTypedPayload<T> payload;
    private readonly CacheLoadCoordinator<CacheSourceResult<T>> loads;

    /// <summary>Creates a typed collection engine for one finalized runtime and trusted typed contract using the host service scope factory.</summary>
    public CacheCollection(CacheCollectionRuntime runtime, CacheValueContract<T> contract, IServiceProvider services)
    {
        this.runtime = runtime;
        this.payload = new(runtime, contract, services);
        this.loads = new(runtime, services.GetRequiredService<IServiceScopeFactory>(), (item, observation, token) => this.ResolveRemoteAsync(item, observation, token),
            new CacheBehaviorFactory(runtime.Catalog, services).Loads(runtime.Binding), (item, token) => this.RecheckAsync(item, token),
            (item, observation, token) => this.ResolveRemoteAsync(item, observation, token, false),
            (item, token) => this.RecheckAsync(item, token, false), services.GetService<CacheDiagnostics>()?.ForCollection(runtime.Binding.Name));
    }

    /// <inheritdoc />
    public string Name => this.runtime.Binding.Name;

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<T>>> GetAsync(string key, CancellationToken cancellationToken = default) =>
        this.payload.GetAsync(key, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> OpenReadAsync(string key, CacheReadOptions options = null,
        CancellationToken cancellationToken = default) => this.payload.OpenEncodedAsync(key, options, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<T>>> GetOrLoadAsync(CacheItem item, CacheLoadDelegate<T> loader,
        CancellationToken cancellationToken = default) => this.LoadAsync(item, loader, new(), false, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<T>>> GetOrLoadAsync(CacheItem item, CacheLoadDelegate<T> loader, CacheAccessOptions options,
        CancellationToken cancellationToken = default) => this.LoadAsync(item, loader, options ?? new(), false, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<T>>> RefreshAsync(CacheItem item, CacheLoadDelegate<T> loader,
        CancellationToken cancellationToken = default) => this.LoadAsync(item, loader, new(), true, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<T>>> RefreshAsync(CacheItem item, CacheLoadDelegate<T> loader, CacheAccessOptions options,
        CancellationToken cancellationToken = default) => this.LoadAsync(item, loader, options ?? new(), true, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheWriteOutcome>> SetAsync(CacheItem item, T value, CacheEntryDescriptor descriptor = null,
        CancellationToken cancellationToken = default) => this.SetAsync(item, value, default(CacheWriteCondition), descriptor, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheWriteOutcome>> SetAsync(CacheItem item, T value, CacheEntryVersion expectedVersion,
        CacheEntryDescriptor descriptor = null, CancellationToken cancellationToken = default) =>
        this.SetAsync(item, value, new CacheWriteCondition(expectedVersion), descriptor, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheWriteOutcome>> SetAsync(CacheItem item, T value, CacheWriteCondition condition,
        CacheEntryDescriptor descriptor = null, CancellationToken cancellationToken = default) =>
        this.OwnMutationAsync(token => this.payload.SetAsync(item, value, descriptor, condition, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheWriteOutcome>> SetEncodedAsync(CacheItem item, Stream input, CacheEntryDescriptor descriptor = null,
        CacheWriteCondition condition = default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return this.OwnMutationAsync(token => this.payload.SetEncodedAsync(item, input, descriptor, condition, token), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result> RemoveAsync(string key, CancellationToken cancellationToken = default) => this.runtime.RemoveAsync(key, default, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> RemoveAsync(string key, CacheEntryVersion expectedVersion, CancellationToken cancellationToken = default) =>
        this.runtime.RemoveAsync(key, new(expectedVersion), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> TouchAsync(string key, CancellationToken cancellationToken = default) => this.runtime.TouchAsync(key, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationPreview>> PreviewInvalidationAsync(CacheSelection selection, CancellationToken cancellationToken = default) =>
        this.runtime.PreviewAsync(selection, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheSelection selection, CancellationToken cancellationToken = default) =>
        this.runtime.InvalidateAsync(selection, false, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationOutcome>> ClearAsync(CancellationToken cancellationToken = default) =>
        this.runtime.InvalidateAsync(CacheSelection.None, true, cancellationToken);

    private async ValueTask<Result<CacheLookup<T>>> LoadAsync(CacheItem item, CacheLoadDelegate<T> loader, CacheAccessOptions options,
        bool refresh, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(loader);
        var itemValidation = CacheSizePolicy.ValidateItem(item, this.runtime.Binding.Options);
        if (itemValidation.IsFailure)
        {
            return Result<CacheLookup<T>>.Failure(itemValidation);
        }

        if (!Enum.IsDefined(options.Mode))
        {
            return Result<CacheLookup<T>>.Failure(new CacheError(CacheErrorCode.Validation, "Unknown cache access mode."));
        }

        if ((refresh || options.Mode == CacheAccessMode.Bypass) && CacheLoadExecutionContext.IsLoading)
        {
            return Result<CacheLookup<T>>.Failure(new CacheError(CacheErrorCode.NestedCacheLoad, "An executing loader cannot refresh or bypass another cache source."));
        }

        CacheEntryVersion? previous = null;
        if (this.runtime.Enabled && options.Mode == CacheAccessMode.Normal)
        {
            if (refresh)
            {
                var current = await this.runtime.Access.CallAsync(token => this.runtime.Access.Provider.LookupAsync(this.runtime.Identity(item.Key),
                    new(ObserveAccess: false), token), cancellationToken).ConfigureAwait(false);
                if (current.IsSuccess)
                {
                    if (options.ExpectedVersion.HasValue && (current.Value.State != CacheReadValidationState.Valid ||
                        current.Value.Metadata?.Version != options.ExpectedVersion))
                    {
                        return Result<CacheLookup<T>>.Failure(new CacheError(CacheErrorCode.Conflict, "The selected refresh version is no longer current and valid."));
                    }

                    if (options.Background && current.Value.State == CacheReadValidationState.Valid && current.Value.Metadata?.IsNotFound == true)
                    {
                        return Result<CacheLookup<T>>.Success(new(false, default, CacheLookupOutcome.Hit, CacheStorageOutcome.None,
                            isNotFound: true, version: current.Value.Metadata.Version));
                    }

                    previous = current.Value.Metadata?.Version;
                }
                else if (!this.CanFallback(current))
                {
                    return Result<CacheLookup<T>>.Failure(current);
                }
            }
            else
            {
                var found = await this.payload.GetAsync(item.Key, cancellationToken, observeAccess: !options.Background).ConfigureAwait(false);
                if (found.IsSuccess && (found.Value.Found || found.Value.IsNotFound))
                {
                    return found;
                }

                if (found.IsFailure && !this.CanFallback(found))
                {
                    return found;
                }
            }
        }

        var loaded = await this.loads.LoadAsync(item, (context, token) => this.LoadOwnedAsync(context, loader, options,
            previous, token), cancellationToken, recheckCache: !refresh && options.Mode == CacheAccessMode.Normal, background: options.Background).ConfigureAwait(false);
        return ToLookup(loaded, options.Mode);
    }

    private async ValueTask<Result<CacheSourceResult<T>>> LoadOwnedAsync(CacheOwnedLoadContext context, CacheLoadDelegate<T> loader,
        CacheAccessOptions options, CacheEntryVersion? previous, CancellationToken cancellationToken)
    {
        if (options.Background && options.ExpectedVersion.HasValue)
        {
            var current = await this.runtime.Access.CallAsync(token => this.runtime.Access.Provider.ValidateReadAsync(context.Public.Identity,
                options.ExpectedVersion.Value, new(ObserveAccess: false), token), cancellationToken).ConfigureAwait(false);
            var policy = this.runtime.Binding.Options.Refresh;
            if (current.IsFailure) { return Result<CacheSourceResult<T>>.Failure(current); }

            if (current.Value.State != CacheReadValidationState.Valid || !CacheRefreshPolicy.IsEligible(current.Value.Metadata,
                new(context.Public.Identity.Namespace, this.Name, context.Public.Identity.ContractVersion, this.runtime.Time.GetUtcNow(), policy.RefreshAhead, policy.ActivityWindow)))
            {
                return Result<CacheSourceResult<T>>.Failure(new CacheError(CacheErrorCode.Conflict, "The selected background version is no longer eligible before source execution."));
            }
        }

        var loaded = await context.InvokeAsync<CacheLoadResult<T>>((loadContext, token) => loader(loadContext, token), cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result<CacheSourceResult<T>>.Failure(loaded);
        }

        if (loaded.Value is null)
        {
            return Result<CacheSourceResult<T>>.Failure(new CacheError(CacheErrorCode.Contract, "The cache loader returned no declared value/absence result."));
        }

        return await this.RetainLoadedAsync(context, loaded.Value, options, previous, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<CacheSourceResult<T>>> RetainLoadedAsync(CacheOwnedLoadContext context, CacheLoadResult<T> value,
        CacheAccessOptions options, CacheEntryVersion? previous, CancellationToken cancellationToken)
    {
        var mode = !this.runtime.Enabled ? CacheAccessMode.Bypass : options.Mode;
        var dimensions = CacheSizePolicy.MergeDimensions(context.Public.Item, value.Descriptor, this.runtime.Binding.Options);
        var descriptor = CacheSizePolicy.ValidateDescriptor(value.Descriptor, this.runtime.Binding.Options);
        var lifetime = CacheExpirationPolicy.Create(this.runtime.Binding.Options, value.Descriptor, value.IsNotFound, this.runtime.Time.GetUtcNow());
        if (dimensions.IsFailure || descriptor.IsFailure || lifetime.IsFailure)
        {
            return Result<CacheSourceResult<T>>.Failure().WithErrors(dimensions.IsFailure ? dimensions.Errors : descriptor.IsFailure ? descriptor.Errors : lifetime.Errors);
        }

        var storage = CacheStorageOutcome.Suppressed;
        CacheEntryVersion? version = null;
        if (mode == CacheAccessMode.Normal && !value.Descriptor.SuppressStorage)
        {
            storage = CacheStorageOutcome.Failed;
            if (context.CanPublish)
            {
                var written = await this.payload.SetAsync(context.Public.Item, value.Value, value.Descriptor,
                    previous.HasValue ? new(previous) : new(CreateOnly: true), cancellationToken, context.Fence, context.Owner,
                    value.IsNotFound, context.Public.Deadline).ConfigureAwait(false);
                if (written.IsSuccess)
                {
                    storage = written.Value.StorageOutcome;
                    version = written.Value.Version;
                }
                else
                {
                    var error = written.Errors.OfType<CacheError>().FirstOrDefault()?.Code;
                    storage = error switch { CacheErrorCode.Conflict => CacheStorageOutcome.Conflict,
                        CacheErrorCode.Capacity => CacheStorageOutcome.Rejected, CacheErrorCode.Indeterminate => CacheStorageOutcome.Indeterminate,
                        _ => CacheStorageOutcome.Failed };
                }
            }
        }

        return Result<CacheSourceResult<T>>.Success(new(value.Value, value.IsNotFound, storage, version, mode));
    }

    private async ValueTask<Result<CacheSourceResult<T>>> ResolveRemoteAsync(CacheItem item, CacheOwnershipObservation observation,
        CancellationToken cancellationToken, bool observeAccess = true)
    {
        var found = await this.payload.GetAsync(item.Key, cancellationToken, observeAccess).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result<CacheSourceResult<T>>.Failure(found);
        }

        if (!(found.Value.Found || found.Value.IsNotFound) || found.Value.Version != observation.Version)
        {
            return Result<CacheSourceResult<T>>.Failure(new CacheError(CacheErrorCode.Conflict, "The remote published version is no longer valid."));
        }

        return Result<CacheSourceResult<T>>.Success(new(found.Value.Value, found.Value.IsNotFound, CacheStorageOutcome.Stored, found.Value.Version));
    }

    private bool CanFallback(IResult result) => this.runtime.Binding.Options.Loading.CoordinationScope == CacheCoordinationScope.NodeLocal &&
        result.Errors.OfType<CacheError>().Any(e => e.Code == CacheErrorCode.ProviderUnavailable);

    private async ValueTask<Result<CacheSourceResult<T>>> RecheckAsync(CacheItem item, CancellationToken cancellationToken, bool observeAccess = true)
    {
        var found = await this.payload.GetAsync(item.Key, cancellationToken, observeAccess).ConfigureAwait(false);
        if (found.IsSuccess && (found.Value.Found || found.Value.IsNotFound))
        {
            return Result<CacheSourceResult<T>>.Success(new(found.Value.Value, found.Value.IsNotFound, CacheStorageOutcome.None, found.Value.Version, Acquired: true));
        }

        return found.IsFailure && !this.CanFallback(found) ? Result<CacheSourceResult<T>>.Failure(found) :
            Result<CacheSourceResult<T>>.Failure(new CacheError(CacheErrorCode.NotFound, "No reusable version was acquired after source admission."));
    }

    private async ValueTask<Result<CacheWriteOutcome>> OwnMutationAsync(Func<CancellationToken, ValueTask<Result<CacheWriteOutcome>>> operation,
        CancellationToken cancellationToken)
    {
        var actual = ExecuteAsync();
        return await this.runtime.WaitOwnedAsync(actual, cancellationToken, this.runtime.Binding.Options.Streaming.Lifetime,
            CacheErrorCode.CoordinationTimeout).ConfigureAwait(false);

        async Task<Result<CacheWriteOutcome>> ExecuteAsync()
        {
            using var timer = new CacheCancellationDeadline(this.runtime.Binding.Options.Streaming.Lifetime, this.runtime.Time, this.runtime.Tasks);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timer.Token, cancellationToken);
            try { return await operation(linked.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.CoordinationTimeout, "The bounded write transfer expired."));
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<Result<CacheLookup<T>>>>> GetOrLoadManyAsync(IReadOnlyList<CacheItem> items,
        CacheLoadDelegate<T> loader, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loader);
        var snapshot = CacheBatchAdmission.Snapshot(items, this.runtime.Binding.Options.Limits.MaxBatchSize, this.runtime.Binding.Options);
        if (snapshot.IsFailure)
        {
            return Result<IReadOnlyList<Result<CacheLookup<T>>>>.Failure(snapshot);
        }

        // Each owned single-key callback runs independently. Waiting on foreign work holds no
        // unfinished callback from this batch, so overlapping batches cannot form a wait cycle.
        var pending = snapshot.Value.Select(item => this.GetOrLoadAsync(item, loader, cancellationToken).AsTask()).ToArray();
        return Result<IReadOnlyList<Result<CacheLookup<T>>>>.Success(Array.AsReadOnly(await Task.WhenAll(pending).ConfigureAwait(false)));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<Result<CacheLookup<T>>>>> GetOrLoadManyAsync(IReadOnlyList<CacheItem> items,
        CacheBatchLoadDelegate<T> loader, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loader);
        var snapshot = CacheBatchAdmission.Snapshot(items, this.runtime.Binding.Options.Limits.MaxBatchSize, this.runtime.Binding.Options);
        if (snapshot.IsFailure)
        {
            return Result<IReadOnlyList<Result<CacheLookup<T>>>>.Failure(snapshot);
        }

        var results = new Result<CacheLookup<T>>[snapshot.Value.Count];
        var missing = new List<CacheItem>();
        var positions = new List<int>();
        for (var index = 0; index < snapshot.Value.Count; index++)
        {
            var found = await this.GetAsync(snapshot.Value[index].Key, cancellationToken).ConfigureAwait(false);
            if ((found.IsSuccess && !(found.Value.Found || found.Value.IsNotFound)) || (found.IsFailure && this.CanFallback(found)))
            {
                missing.Add(snapshot.Value[index]);
                positions.Add(index);
            }
            else
            {
                results[index] = found;
            }
        }

        if (missing.Count != 0)
        {
            var loaded = await this.loads.LoadBatchAsync(missing, ExecuteAsync, cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < missing.Count; index++)
            {
                results[positions[index]] = loaded.IsFailure ? Result<CacheLookup<T>>.Failure(loaded) : ToLookup(loaded.Value[index]);
            }
        }

        return Result<IReadOnlyList<Result<CacheLookup<T>>>>.Success(Array.AsReadOnly(results));

        async ValueTask<Result> ExecuteAsync(IReadOnlyList<CacheOwnedLoadContext> contexts,
            Func<string, Result<CacheSourceResult<T>>, ValueTask<Result>> complete, CancellationToken token)
        {
            var byKey = contexts.ToDictionary(c => c.Public.Identity.Key, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var context = new CacheBatchLoadContext(Array.AsReadOnly(contexts.Select(c => c.Public).ToArray()),
                contexts[0].Public.Services, contexts.Min(c => c.Public.Deadline));
            var hasYield = false;
            return await contexts[0].InvokeBatchAsync(async attemptToken =>
            {
                IResult sourceFailure = null;
                var output = loader(context, attemptToken);
                if (output is null)
                {
                    return Result.Failure(new CacheError(CacheErrorCode.Contract, "The batch source returned no completion sequence."));
                }

                await foreach (var item in output.WithCancellation(attemptToken).ConfigureAwait(false))
                {
                    attemptToken.ThrowIfCancellationRequested();
                    if (item is null)
                    {
                        return Result.Failure(new CacheError(CacheErrorCode.Contract, "The batch source returned a null completion."));
                    }

                    hasYield = true;
                    var key = CacheIdentityNormalizer.Key(item.Key);
                    if (!byKey.TryGetValue(key, out var owner) || !seen.Add(key))
                    {
                        return await complete(key, Result<CacheSourceResult<T>>.Failure(new CacheError(CacheErrorCode.Contract,
                            "The batch source returned an unknown or duplicate key."))).ConfigureAwait(false);
                    }

                    Result<CacheSourceResult<T>> retained;
                    if (item.Result.IsFailure)
                    {
                        sourceFailure ??= item.Result;
                        retained = Result<CacheSourceResult<T>>.Failure(item.Result);
                    }
                    else if (item.Result.Value is null)
                    {
                        retained = Result<CacheSourceResult<T>>.Failure(new CacheError(CacheErrorCode.Contract, "The batch source returned no value/absence descriptor."));
                    }
                    else
                    {
                        using var linked = CancellationTokenSource.CreateLinkedTokenSource(attemptToken, owner.InterestToken);
                        try
                        {
                            retained = await this.RetainLoadedAsync(owner, item.Result.Value, new(), null, linked.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (owner.InterestToken.IsCancellationRequested && !attemptToken.IsCancellationRequested)
                        {
                            retained = Result<CacheSourceResult<T>>.Failure(new CacheError(CacheErrorCode.SourceRejected, "This batch item has no remaining interested caller."));
                        }
                    }

                    var completed = await complete(key, retained).ConfigureAwait(false);
                    if (completed.IsFailure)
                    {
                        return completed;
                    }
                }

                return sourceFailure is null ? Result.Success() : Result.Failure().WithErrors(sourceFailure.Errors);
            }, token, () => !hasYield).ConfigureAwait(false);
        }
    }

    private static Result<CacheLookup<T>> ToLookup(Result<CacheFlightCompletion<CacheSourceResult<T>>> loaded,
        CacheAccessMode accessMode = CacheAccessMode.Normal)
    {
        if (loaded.IsFailure)
        {
            return Result<CacheLookup<T>>.Failure(loaded);
        }

        var source = loaded.Value.Value;
        return Result<CacheLookup<T>>.Success(new(!source.IsNotFound, source.Value,
            source.Acquired ? CacheLookupOutcome.Hit : loaded.Value.Joined ? CacheLookupOutcome.Joined :
                source.Mode == CacheAccessMode.Bypass ? CacheLookupOutcome.Bypassed : CacheLookupOutcome.Loaded,
            source.StorageOutcome, accessMode, source.IsNotFound, source.Version));
    }
}