// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Implements declared raw binary cache operations whose source loads return a stream only after confirmed store publication.</summary>
/// <example><code>var reports = cache.GetBinaryCollection(&quot;reports&quot;); var result = await reports.OpenReadAsync(&quot;report:42&quot;);</code></example>
public sealed class CacheBinaryCollection : ICacheBinaryCollection
{
    private readonly CacheCollectionRuntime runtime;
    private readonly CacheBinaryValueContract contract;
    private readonly CacheLoadCoordinator<CacheSourceResult<bool>> loads;

    /// <summary>Creates a binary collection engine for one finalized runtime and declared raw format using host-owned service scopes.</summary>
    public CacheBinaryCollection(CacheCollectionRuntime runtime, CacheBinaryValueContract contract, IServiceProvider services)
    {
        this.runtime = runtime;
        this.contract = contract;
        this.loads = new(runtime, services.GetRequiredService<IServiceScopeFactory>(), this.ResolveRemoteAsync,
            new CacheBehaviorFactory(runtime.Catalog, services).Loads(runtime.Binding), this.RecheckAsync,
            observations: services.GetService<CacheDiagnostics>()?.ForCollection(runtime.Binding.Name));
    }

    /// <inheritdoc />
    public string Name => this.runtime.Binding.Name;

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> OpenReadAsync(string key, CacheReadOptions options = null,
        CancellationToken cancellationToken = default) => this.runtime.OpenAsync(key, options, cancellationToken, compatible: this.Compatible);

    /// <inheritdoc />
    public ValueTask<Result<CacheWriteOutcome>> SetAsync(CacheItem item, Stream input, CacheEntryDescriptor descriptor = null,
        CacheWriteCondition condition = default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (descriptor?.SuppressStorage == true)
        {
            return ValueTask.FromResult(Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.Unsupported, "Binary writes require confirmed storage.")));
        }

        var actual = ExecuteAsync();
        return this.runtime.WaitOwnedAsync(actual, cancellationToken, this.runtime.Binding.Options.Streaming.Lifetime, CacheErrorCode.CoordinationTimeout);

        async Task<Result<CacheWriteOutcome>> ExecuteAsync()
        {
            using var timer = new CacheCancellationDeadline(this.runtime.Binding.Options.Streaming.Lifetime, this.runtime.Time, this.runtime.Tasks);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timer.Token, cancellationToken);
            try
            {
                return await this.runtime.WriteAsync(item, descriptor, condition, this.contract.FormatId, this.contract.FormatVersion,
                    (stream, token) => this.runtime.CopyInputAsync(input, stream, token), this.contract.Validator, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.CoordinationTimeout, "The bounded binary write expired."));
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> GetOrLoadAsync(CacheItem item, CacheBinaryLoadDelegate loader,
        CancellationToken cancellationToken = default) => this.LoadAsync(item, loader, new(), false, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> GetOrLoadAsync(CacheItem item, CacheBinaryLoadDelegate loader, CacheAccessOptions options,
        CancellationToken cancellationToken = default) => this.LoadAsync(item, loader, options ?? new(), false, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> RefreshAsync(CacheItem item, CacheBinaryLoadDelegate loader,
        CancellationToken cancellationToken = default) => this.LoadAsync(item, loader, new(), true, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> RefreshAsync(CacheItem item, CacheBinaryLoadDelegate loader, CacheAccessOptions options,
        CancellationToken cancellationToken = default) => this.LoadAsync(item, loader, options ?? new(), true, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> RemoveAsync(string key, CacheWriteCondition condition = default, CancellationToken cancellationToken = default) =>
        this.runtime.RemoveAsync(key, condition, cancellationToken);

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

    private async ValueTask<Result<CacheLookup<ICacheReadHandle>>> LoadAsync(CacheItem item, CacheBinaryLoadDelegate loader,
        CacheAccessOptions options, bool refresh, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(loader);
        var itemValidation = CacheSizePolicy.ValidateItem(item, this.runtime.Binding.Options);
        if (itemValidation.IsFailure)
        {
            return Result<CacheLookup<ICacheReadHandle>>.Failure(itemValidation);
        }

        if (options.Mode != CacheAccessMode.Normal || !this.runtime.Enabled)
        {
            return Result<CacheLookup<ICacheReadHandle>>.Failure(new CacheError(CacheErrorCode.Unsupported, "Binary source loading requires enabled, normally retained collection access."));
        }

        if (refresh && CacheLoadExecutionContext.IsLoading)
        {
            return Result<CacheLookup<ICacheReadHandle>>.Failure(new CacheError(CacheErrorCode.NestedCacheLoad, "An executing cache loader cannot refresh a binary source."));
        }

        CacheEntryVersion? previous = null;
        if (!refresh)
        {
            var found = await this.runtime.OpenAsync(item.Key, null, cancellationToken, observeAccess: !options.Background, compatible: this.Compatible).ConfigureAwait(false);
            if (found.IsFailure || found.Value.Found || found.Value.IsNotFound)
            {
                return found;
            }
        }
        else
        {
            var current = await this.runtime.Access.CallAsync(token => this.runtime.Access.Provider.LookupAsync(this.runtime.Identity(item.Key),
                new(ObserveAccess: false), token), cancellationToken).ConfigureAwait(false);
            if (current.IsFailure)
            {
                return Result<CacheLookup<ICacheReadHandle>>.Failure(current);
            }

            if (options.ExpectedVersion.HasValue && (current.Value.State != CacheReadValidationState.Valid ||
                current.Value.Metadata?.Version != options.ExpectedVersion))
            {
                return Result<CacheLookup<ICacheReadHandle>>.Failure(new CacheError(CacheErrorCode.Conflict, "The selected refresh version is no longer current and valid."));
            }

            if (options.Background && current.Value.State == CacheReadValidationState.Valid && current.Value.Metadata?.IsNotFound == true)
            {
                return Result<CacheLookup<ICacheReadHandle>>.Success(new(false, null, CacheLookupOutcome.Hit, CacheStorageOutcome.None,
                    isNotFound: true, version: current.Value.Metadata.Version));
            }

            previous = current.Value.Metadata?.Version;
        }

        var loaded = await this.loads.LoadAsync(item, (context, token) => this.LoadOwnedAsync(context, loader, previous, token, options.Background ? options.ExpectedVersion : null), cancellationToken,
            recheckCache: !refresh, background: options.Background).ConfigureAwait(false);
        return await this.DeliverAsync(item.Key, loaded, cancellationToken, observeAccess: !options.Background).ConfigureAwait(false);
    }

    private async ValueTask<Result<CacheSourceResult<bool>>> LoadOwnedAsync(CacheOwnedLoadContext context, CacheBinaryLoadDelegate loader,
        CacheEntryVersion? previous, CancellationToken cancellationToken, CacheEntryVersion? requiredVersion = null)
    {
        if (!context.CanPublish)
        {
            return Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "Binary source work has no confirmed publication authority."));
        }

        if (requiredVersion.HasValue)
        {
            var current = await this.runtime.Access.CallAsync(token => this.runtime.Access.Provider.ValidateReadAsync(context.Public.Identity,
                requiredVersion.Value, new(ObserveAccess: false), token), cancellationToken).ConfigureAwait(false);
            var policy = this.runtime.Binding.Options.Refresh;
            if (current.IsFailure) { return Result<CacheSourceResult<bool>>.Failure(current); }

            if (current.Value.State != CacheReadValidationState.Valid || !CacheRefreshPolicy.IsEligible(current.Value.Metadata,
                new(context.Public.Identity.Namespace, this.Name, context.Public.Identity.ContractVersion, this.runtime.Time.GetUtcNow(), policy.RefreshAhead, policy.ActivityWindow)))
            {
                return Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.Conflict, "The selected background binary version is no longer eligible before source execution."));
            }
        }

        for (var attempt = 1; ; attempt++)
        {
            CacheBinaryLoadResult absence = null;
            var admission = await this.runtime.CollectionResources.Transfers.AcquireAsync(this.runtime.Binding.Options.Loading.WaitTimeout,
                cancellationToken).ConfigureAwait(false);
            if (admission.IsFailure)
            {
                return Result<CacheSourceResult<bool>>.Failure(admission);
            }

            using (admission.Value)
            {
                var request = new CacheProviderWriteRequest(context.Public.Identity, context.Fence.Value,
                    previous.HasValue ? new(previous) : new(CreateOnly: true), CacheSizePolicy.MaximumStoredBytes(this.runtime.Binding.Options),
                    context.Owner, Deadline: context.Public.Deadline);
                var created = await CachePayloadStage.CreateAsync(this.runtime.Access.Provider, request, this.runtime.Binding, this.runtime.Resources,
                    this.runtime.Time, context.Public.Item, this.contract.FormatId, this.contract.FormatVersion, cancellationToken,
                    this.runtime.Access, this.runtime.Tasks, this.runtime.PayloadPipeline, this.runtime.Transforms).ConfigureAwait(false);
                if (created.IsFailure)
                {
                    return Result<CacheSourceResult<bool>>.Failure(created);
                }

                await using var stage = created.Value;
                var loaded = await context.InvokeAsync<CacheBinaryLoadResult>((loadContext, token) => loader(loadContext, stage.Stream, token),
                    cancellationToken, singleAttempt: true).ConfigureAwait(false);
                if (loaded.IsFailure)
                {
                    if (attempt >= this.runtime.Binding.Options.Loading.MaxAttempts || !CacheSourceFailureClassifier.IsSourceFailure(loaded))
                    {
                        return Result<CacheSourceResult<bool>>.Failure(loaded);
                    }
                }
                else if (loaded.Value is null || !stage.Stream.CompletedSuccessfully)
                {
                    return Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.Contract, "The binary loader closed or failed its cache-owned destination/result."));
                }
                else if (loaded.Value.Descriptor.SuppressStorage)
                {
                    return Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.Unsupported, "A binary loader cannot suppress publication."));
                }
                else if (loaded.Value.IsNotFound)
                {
                    if (stage.Stream.Written != 0)
                    {
                        return Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.Contract, "Confirmed binary absence cannot contain payload bytes."));
                    }

                    absence = loaded.Value;
                }
                else
                {
                    var completed = await stage.CompleteAsync(loaded.Value.Descriptor, this.contract.Validator, cancellationToken).ConfigureAwait(false);
                    if (completed.IsFailure)
                    {
                        return Result<CacheSourceResult<bool>>.Failure(completed);
                    }

                    if (!context.CanPublish)
                    {
                        return Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "Shared source authority was lost before binary publication."));
                    }

                    var retained = this.runtime.Hot?.GetVersion(context.Public.Identity);
                    var published = await stage.PublishAsync(cancellationToken).ConfigureAwait(false);
                    if (retained.HasValue && published.IsSuccess && published.Value.StorageOutcome == CacheStorageOutcome.Stored)
                    {
                        this.runtime.Hot.Retire(context.Public.Identity, retained.Value);
                    }

                    return RequirePublication(published);
                }
            }

            if (absence is not null)
            {
                if (!context.CanPublish)
                {
                    return Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "Binary absence lost its publication authority."));
                }

                var marker = await this.runtime.WriteAsync(context.Public.Item, absence.Descriptor,
                    previous.HasValue ? new(previous) : new(CreateOnly: true), this.contract.FormatId, this.contract.FormatVersion,
                    null, null, cancellationToken, context.Fence, context.Owner, true, context.Public.Deadline).ConfigureAwait(false);
                if (marker.IsFailure)
                {
                    return Result<CacheSourceResult<bool>>.Failure(marker);
                }

                return Result<CacheSourceResult<bool>>.Success(new(false, true, marker.Value.StorageOutcome, marker.Value.Version));
            }

            await Task.Delay(this.runtime.Binding.Options.Loading.RetryDelay, this.runtime.Time, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<CacheSourceResult<bool>>> RecheckAsync(CacheItem item, CancellationToken cancellationToken)
    {
        var found = await this.runtime.Access.CallAsync(token => this.runtime.Access.Provider.LookupAsync(this.runtime.Identity(item.Key),
            new(ObserveAccess: false), token), cancellationToken).ConfigureAwait(false);
        if (found.IsFailure) { return Result<CacheSourceResult<bool>>.Failure(found); }

        var metadata = found.Value.Metadata;
        return found.Value.State == CacheReadValidationState.Valid && this.Compatible(metadata) ?
            Result<CacheSourceResult<bool>>.Success(new(!metadata.IsNotFound, metadata.IsNotFound, CacheStorageOutcome.None, metadata.Version, Acquired: true)) :
            Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.NotFound, "No reusable binary version was acquired after source admission."));
    }

    private async ValueTask<Result<CacheLookup<ICacheReadHandle>>> DeliverAsync(string key, Result<CacheFlightCompletion<CacheSourceResult<bool>>> loaded,
        CancellationToken cancellationToken, bool immediate = false, bool observeAccess = true)
    {
        if (loaded.IsFailure)
        {
            return Result<CacheLookup<ICacheReadHandle>>.Failure(loaded);
        }

        var source = loaded.Value.Value;
        var outcome = source.Acquired ? CacheLookupOutcome.Hit : loaded.Value.Joined ? CacheLookupOutcome.Joined : CacheLookupOutcome.Loaded;
        if (source.IsNotFound)
        {
            return Result<CacheLookup<ICacheReadHandle>>.Success(new(false, null, outcome, source.StorageOutcome,
                isNotFound: true, version: source.Version));
        }

        var opened = await this.runtime.OpenAsync(key, new(ExpectedVersion: source.Version), cancellationToken, observeAccess: observeAccess, compatible: this.Compatible,
            waitForTransfer: !immediate).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return opened;
        }

        return opened.Value.Found ? Result<CacheLookup<ICacheReadHandle>>.Success(new(true, opened.Value.Value, outcome,
            source.StorageOutcome, version: opened.Value.Version)) : Result<CacheLookup<ICacheReadHandle>>.Failure(new CacheError(
                CacheErrorCode.Conflict, "The confirmed binary generation is no longer valid for acquisition."));
    }

    private ValueTask<Result<CacheSourceResult<bool>>> ResolveRemoteAsync(CacheItem item, CacheOwnershipObservation observation,
        CancellationToken cancellationToken) => ValueTask.FromResult(observation.State == CacheOwnershipState.Published && observation.Version.HasValue ?
            Result<CacheSourceResult<bool>>.Success(new(!observation.IsNotFound, observation.IsNotFound, CacheStorageOutcome.Stored, observation.Version)) :
            observation.IsNotFound ? Result<CacheSourceResult<bool>>.Success(new(false, true, CacheStorageOutcome.Suppressed, null)) :
                Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.Incomplete, "The remote binary source did not confirm publication.")));

    private bool Compatible(CacheEntryMetadata metadata) => metadata.IsNotFound || (metadata.EnvelopeVersion is 1 or 2 &&
        metadata.CodecId == this.contract.FormatId && metadata.CodecVersion == this.contract.FormatVersion);

    private static Result<CacheSourceResult<bool>> RequirePublication(Result<CacheWriteOutcome> published)
    {
        if (published.IsFailure)
        {
            return Result<CacheSourceResult<bool>>.Failure(published);
        }

        return published.Value.StorageOutcome == CacheStorageOutcome.Stored && published.Value.Version.HasValue ?
            Result<CacheSourceResult<bool>>.Success(new(true, false, CacheStorageOutcome.Stored, published.Value.Version)) :
            Result<CacheSourceResult<bool>>.Failure(new CacheError(published.Value.StorageOutcome switch
            {
                CacheStorageOutcome.Indeterminate => CacheErrorCode.Indeterminate,
                CacheStorageOutcome.Conflict => CacheErrorCode.Conflict,
                CacheStorageOutcome.Rejected => CacheErrorCode.Capacity,
                CacheStorageOutcome.Suppressed => CacheErrorCode.Unsupported,
                _ => CacheErrorCode.ProviderUnavailable
            }, "Binary source publication was not confirmed."));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>>> GetOrLoadManyAsync(IReadOnlyList<CacheItem> items,
        CacheBinaryLoadDelegate loader, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loader);
        return this.LoadBatchCoreAsync(items, async (missing, token) =>
        {
            var pending = missing.Select(item => this.loads.LoadAsync(item, (context, ownedToken) =>
                this.LoadOwnedAsync(context, loader, null, ownedToken), token).AsTask()).ToArray();
            return Result<IReadOnlyList<Result<CacheFlightCompletion<CacheSourceResult<bool>>>>>.Success(
                Array.AsReadOnly(await Task.WhenAll(pending).ConfigureAwait(false)));
        }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>>> GetOrLoadManyAsync(IReadOnlyList<CacheItem> items,
        CacheBinaryBatchLoadDelegate loader, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loader);
        return this.LoadBatchCoreAsync(items, (missing, token) => this.loads.LoadBatchAsync(missing,
            (contexts, complete, ownedToken) => this.ExecuteBatchSourceAsync(contexts, complete, loader, ownedToken), token), cancellationToken);
    }

    private async ValueTask<Result<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>>> LoadBatchCoreAsync(IReadOnlyList<CacheItem> items,
        Func<IReadOnlyList<CacheItem>, CancellationToken, ValueTask<Result<IReadOnlyList<Result<CacheFlightCompletion<CacheSourceResult<bool>>>>>>> load,
        CancellationToken cancellationToken)
    {
        var snapshot = CacheBatchAdmission.Snapshot(items, this.runtime.Binding.Options.Limits.MaxBatchSize, this.runtime.Binding.Options);
        if (snapshot.IsFailure)
        {
            return Result<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>>.Failure(snapshot);
        }

        if (!this.runtime.Enabled)
        {
            return Result<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>>.Failure(new CacheError(CacheErrorCode.Unsupported,
                "Binary batch source loading requires enabled retained collection access."));
        }

        var receipts = new Result<CacheFlightCompletion<CacheSourceResult<bool>>>[snapshot.Value.Count];
        var missing = new List<CacheItem>();
        var positions = new List<int>();
        for (var index = 0; index < snapshot.Value.Count; index++)
        {
            var item = snapshot.Value[index];
            var current = await this.runtime.Access.CallAsync(token => this.runtime.Access.Provider.LookupAsync(this.runtime.Identity(item.Key),
                new(ObserveAccess: false), token), cancellationToken).ConfigureAwait(false);
            if (current.IsFailure)
            {
                receipts[index] = Result<CacheFlightCompletion<CacheSourceResult<bool>>>.Failure(current);
            }
            else if (current.Value.State == CacheReadValidationState.Valid && this.Compatible(current.Value.Metadata))
            {
                var metadata = current.Value.Metadata;
                receipts[index] = Result<CacheFlightCompletion<CacheSourceResult<bool>>>.Success(new(new(!metadata.IsNotFound,
                    metadata.IsNotFound, CacheStorageOutcome.None, metadata.Version, Acquired: true), false));
            }
            else
            {
                missing.Add(item);
                positions.Add(index);
            }
        }

        if (missing.Count != 0)
        {
            var loaded = await load(missing, cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < missing.Count; index++)
            {
                receipts[positions[index]] = loaded.IsFailure ? Result<CacheFlightCompletion<CacheSourceResult<bool>>>.Failure(loaded) : loaded.Value[index];
            }
        }

        var results = new Result<CacheLookup<ICacheReadHandle>>[snapshot.Value.Count];
        var handedOff = false;
        try
        {
            for (var index = 0; index < results.Length; index++)
            {
                results[index] = await this.DeliverAsync(snapshot.Value[index].Key, receipts[index], cancellationToken, immediate: true).ConfigureAwait(false);
            }

            handedOff = true;
            return Result<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>>.Success(Array.AsReadOnly(results));
        }
        finally
        {
            if (!handedOff)
            {
                foreach (var result in results)
                {
                    if (result.IsSuccess && result.Value.Value is not null)
                    {
                        await result.Value.Value.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }
    }

    private async ValueTask<Result> ExecuteBatchSourceAsync(IReadOnlyList<CacheOwnedLoadContext> contexts,
        Func<string, Result<CacheSourceResult<bool>>, ValueTask<Result>> complete, CacheBinaryBatchLoadDelegate loader,
        CancellationToken cancellationToken)
    {
        var owners = contexts.ToDictionary(c => c.Public.Identity.Key, StringComparer.Ordinal);
        var stages = new Dictionary<string, BinaryBatchStage>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var opening = new HashSet<string>(StringComparer.Ordinal);
        var stateGate = new Lock();
        var finished = false;
        var context = new CacheBinaryBatchLoadContext(Array.AsReadOnly(contexts.Select(c => c.Public).ToArray()),
            contexts[0].Public.Services, contexts.Min(c => c.Public.Deadline), OpenAsync);
        try
        {
            var hasYield = false;
            return await contexts[0].InvokeBatchAsync(async attemptToken =>
            {
                try
                {
                    IResult sourceFailure = null;
                    var output = loader(context, attemptToken);
                    if (output is null)
                    {
                        return Result.Failure(new CacheError(CacheErrorCode.Contract, "The binary batch source returned no completion sequence."));
                    }

                    await foreach (var item in output.WithCancellation(attemptToken).ConfigureAwait(false))
                    {
                        attemptToken.ThrowIfCancellationRequested();
                        if (item is null)
                        {
                            return Result.Failure(new CacheError(CacheErrorCode.Contract, "The binary batch source returned a null completion."));
                        }

                        hasYield = true;
                        if (item.Result.IsFailure)
                        {
                            sourceFailure ??= item.Result;
                        }

                        var key = CacheIdentityNormalizer.Key(item.Key);
                        var duplicate = false;
                        lock (stateGate) { duplicate = !seen.Add(key); }

                        if (!owners.TryGetValue(key, out var owner) || duplicate)
                        {
                            return await complete(key, Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.Contract,
                                "The binary batch source returned an unknown or duplicate key."))).ConfigureAwait(false);
                        }

                        using var linked = CancellationTokenSource.CreateLinkedTokenSource(attemptToken, owner.InterestToken);
                        Result<CacheSourceResult<bool>> retained;
                        try
                        {
                            retained = await FinishAsync(key, owner, item.Result, linked.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (owner.InterestToken.IsCancellationRequested && !attemptToken.IsCancellationRequested)
                        {
                            retained = Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.SourceRejected, "This binary batch item has no remaining interested caller."));
                        }

                        var completed = await complete(key, retained).ConfigureAwait(false);
                        if (completed.IsFailure)
                        {
                            return completed;
                        }
                    }

                    return sourceFailure is null ? Result.Success() : Result.Failure().WithErrors(sourceFailure.Errors);

                }
                finally
                {
                    if (!hasYield)
                    {
                        await ClearStagesAsync(false).ConfigureAwait(false);
                    }
                }
            }, cancellationToken, () => !hasYield).ConfigureAwait(false);
        }
        finally
        {
            await ClearStagesAsync(true).ConfigureAwait(false);
        }

        ValueTask<Result<Stream>> OpenAsync(string key, CancellationToken token) => OpenOwnedAsync(key, token, false);

        async ValueTask<Result<Stream>> OpenOwnedAsync(string key, CancellationToken token, bool completing)
        {
            key = CacheIdentityNormalizer.Key(key);
            if (!owners.TryGetValue(key, out var owner))
            {
                return Result<Stream>.Failure(new CacheError(CacheErrorCode.Contract, "The destination must be a previously unopened owned batch key."));
            }

            if (!owner.CanPublish)
            {
                return Result<Stream>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "The binary batch item has no confirmed publication authority."));
            }

            lock (stateGate)
            {
                if (finished || (!completing && seen.Contains(key)) || stages.ContainsKey(key) || !opening.Add(key))
                {
                    return Result<Stream>.Failure(new CacheError(CacheErrorCode.Contract, "The binary batch destination is completed or already open."));
                }
            }

            if (!this.runtime.CollectionResources.Transfers.TryAcquire(out var transfer))
            {
                lock (stateGate) { opening.Remove(key); }

                return Result<Stream>.Failure(new CacheError(CacheErrorCode.Capacity, "Finish an admitted binary batch destination before opening another."));
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken, owner.InterestToken);
            var handedOff = false;
            try
            {
                var request = new CacheProviderWriteRequest(owner.Public.Identity, owner.Fence.Value, new(CreateOnly: true),
                    CacheSizePolicy.MaximumStoredBytes(this.runtime.Binding.Options), owner.Owner, Deadline: owner.Public.Deadline);
                var created = await CachePayloadStage.CreateAsync(this.runtime.Access.Provider, request, this.runtime.Binding, this.runtime.Resources,
                    this.runtime.Time, owner.Public.Item, this.contract.FormatId, this.contract.FormatVersion, linked.Token,
                    this.runtime.Access, this.runtime.Tasks, this.runtime.PayloadPipeline, this.runtime.Transforms).ConfigureAwait(false);
                if (created.IsFailure)
                {
                    return Result<Stream>.Failure(created);
                }

                lock (stateGate)
                {
                    if (!finished)
                    {
                        stages.Add(key, new(created.Value, transfer));
                        handedOff = true;
                    }
                }

                if (!handedOff)
                {
                    await created.Value.DisposeAsync().ConfigureAwait(false);
                    return Result<Stream>.Failure(new CacheError(CacheErrorCode.Contract, "The binary batch source already ended."));
                }

                return Result<Stream>.Success(created.Value.Stream);
            }
            finally
            {
                lock (stateGate) { opening.Remove(key); }

                if (!handedOff)
                {
                    transfer.Dispose();
                }
            }
        }

        async ValueTask<Result<CacheSourceResult<bool>>> FinishAsync(string key, CacheOwnedLoadContext owner,
            Result<CacheBinaryLoadResult> result, CancellationToken token)
        {
            BinaryBatchStage stage;
            lock (stateGate)
            {
                if (opening.Contains(key))
                {
                    return Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.Contract, "The source yielded a destination before its acquisition completed."));
                }

                stages.TryGetValue(key, out stage);
            }

            try
            {
                if (result.IsFailure)
                {
                    return Result<CacheSourceResult<bool>>.Failure(result);
                }

                if (result.Value is null || result.Value.Descriptor.SuppressStorage)
                {
                    return Result<CacheSourceResult<bool>>.Failure(new CacheError(result.Value is null ? CacheErrorCode.Contract :
                        CacheErrorCode.Unsupported, "The binary batch completion does not permit retained publication."));
                }

                if (result.Value.IsNotFound)
                {
                    if (stage is not null)
                    {
                        if (stage.Stage.Stream.Written != 0 || !stage.Stage.Stream.CompletedSuccessfully)
                        {
                            return Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.Contract, "Binary absence must leave its destination open and contain no payload bytes."));
                        }

                        await stage.DisposeAsync().ConfigureAwait(false);
                        lock (stateGate) { stages.Remove(key); }

                        stage = null;
                    }

                    if (!owner.CanPublish)
                    {
                        return Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "Binary absence has no confirmed authority."));
                    }

                    var marker = await this.runtime.WriteAsync(owner.Public.Item, result.Value.Descriptor, new(CreateOnly: true), this.contract.FormatId,
                        this.contract.FormatVersion, null, null, token, owner.Fence, owner.Owner, true, owner.Public.Deadline).ConfigureAwait(false);
                    return marker.IsSuccess ? Result<CacheSourceResult<bool>>.Success(new(false, true, marker.Value.StorageOutcome, marker.Value.Version)) :
                        Result<CacheSourceResult<bool>>.Failure(marker);
                }

                if (stage is null)
                {
                    // A positive zero-byte result still needs a confirmed empty payload generation.
                    var opened = await OpenOwnedAsync(key, token, true).ConfigureAwait(false);
                    if (opened.IsFailure)
                    {
                        return Result<CacheSourceResult<bool>>.Failure(opened);
                    }

                    lock (stateGate) { stage = stages[key]; }
                }

                var completed = await stage.Stage.CompleteAsync(result.Value.Descriptor, this.contract.Validator, token).ConfigureAwait(false);
                if (completed.IsFailure)
                {
                    return Result<CacheSourceResult<bool>>.Failure(completed);
                }

                if (!owner.CanPublish)
                {
                    return Result<CacheSourceResult<bool>>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "Binary batch authority was lost before publication."));
                }

                return RequirePublication(await stage.Stage.PublishAsync(token).ConfigureAwait(false));
            }
            finally
            {
                if (stage is not null)
                {
                    await stage.DisposeAsync().ConfigureAwait(false);
                    lock (stateGate) { stages.Remove(key); }
                }
            }
        }

        async ValueTask ClearStagesAsync(bool finish)
        {
            BinaryBatchStage[] abandoned;
            lock (stateGate)
            {
                finished |= finish;
                abandoned = stages.Values.ToArray();
                stages.Clear();
            }

            foreach (var stage in abandoned)
            {
                await stage.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private sealed class BinaryBatchStage(CachePayloadStage stage, CacheAdmissionLease transfer) : IAsyncDisposable
    {
        public CachePayloadStage Stage => stage;

        public async ValueTask DisposeAsync()
        {
            try { await stage.DisposeAsync().ConfigureAwait(false); }
            finally { transfer.Dispose(); }
        }
    }
}