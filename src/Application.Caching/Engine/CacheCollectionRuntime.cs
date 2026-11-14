// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Owns one declared collection&apos;s provider access, resource accounts, hot validation and bounded payload operations.</summary>
/// <example><code>var identity = runtime.Identity(&quot;sku:42&quot;);</code></example>
public sealed class CacheCollectionRuntime
{
    /// <summary>Creates collection-scoped runtime accounts and provider access, enabling local hot retention only when all startup opt-ins permit it.</summary>
    public CacheCollectionRuntime(CacheCollectionBinding binding, ICacheStoreProvider provider, CacheCatalog catalog,
        CacheRuntimeResources resources, CacheProviderInitialization initialization, CacheRuntimeTasks tasks, TimeProvider time, CacheObservationSlot observations = null)
    {
        this.Binding = binding;
        this.Catalog = catalog;
        this.Resources = resources;
        this.CollectionResources = resources.ForCollection(binding.Name);
        this.Tasks = tasks;
        this.Time = time;
        this.Observations = observations;
        this.Access = new(binding, provider, resources, initialization, tasks, time, observations);
        if (catalog.Options.Enabled && binding.Options.Enabled && binding.Options.HotEntries.Enabled)
        {
            this.Hot = new(binding.Options.HotEntries, this.CollectionResources, time, observations);
            _ = tasks.Stopping.UnsafeRegister(static state => ((CacheHotStore)state).Dispose(), this.Hot);
        }
    }

    /// <summary>Gets the finalized collection contract, provider binding and effective policies.</summary>
    public CacheCollectionBinding Binding { get; }

    /// <summary>Gets the immutable process startup catalog used by this runtime.</summary>
    public CacheCatalog Catalog { get; }

    /// <summary>Gets exact process-level admission and memory accounts shared by all collections.</summary>
    public CacheRuntimeResources Resources { get; }

    /// <summary>Gets this collection&apos;s child accounts within applicable provider and process ceilings.</summary>
    public CacheCollectionResources CollectionResources { get; }

    /// <summary>Gets the lifetime tracker for actually unfinished admitted work and native tails.</summary>
    public CacheRuntimeTasks Tasks { get; }

    /// <summary>Gets the host observation clock used for validity, deadlines and bounded waits.</summary>
    public TimeProvider Time { get; }

    /// <summary>Gets bounded complete-provider access and lazy initialization for this collection.</summary>
    public CacheProviderAccess Access { get; }

    /// <summary>Gets or sets the code-composed staged-payload behavior pipeline before the handle is exposed.</summary>
    public CachePayloadPipeline PayloadPipeline { get; set; }

    /// <summary>Gets or sets trusted persisted payload transformations resolved for this collection.</summary>
    public CachePayloadTransformRegistry Transforms { get; set; }

    /// <summary>Gets strict local retained representations, or null when local hot retention is disabled.</summary>
    public CacheHotStore Hot { get; }

    /// <summary>Gets the preallocated collection observation slot without introducing key-specific metric labels.</summary>
    public CacheObservationSlot Observations { get; }

    /// <summary>Gets or sets the code-composed observers whose scopes end at actual stream completion.</summary>
    public IReadOnlyList<ICacheTransferObserver> TransferObservers { get; set; } = [];

    /// <summary>Gets whether both global and collection-level advanced cache storage are enabled.</summary>
    public bool Enabled => this.Catalog.Options.Enabled && this.Binding.Options.Enabled;

    /// <summary>Builds the canonical declared cache identity and rejects keys exceeding this collection&apos;s UTF-8 bound.</summary>
    public CacheEntryIdentity Identity(string key)
    {
        var identity = new CacheEntryIdentity(this.Catalog.Options.Namespace, this.Binding.Name, this.Binding.Options.ContractVersion, key);
        if (this.Binding.Options.Limits.MaxKeyBytes < 4096 && System.Text.Encoding.UTF8.GetByteCount(key) > this.Binding.Options.Limits.MaxKeyBytes)
        {
            throw new ArgumentException("The key exceeds this collection's UTF-8 bound.", nameof(key));
        }

        return identity;
    }

    /// <summary>Acquires an authoritative compatible snapshot under transfer admission, preserving reader lifetime and validating every hot reuse.</summary>
    public async ValueTask<Result<CacheLookup<ICacheReadHandle>>> OpenAsync(string key, CacheReadOptions options,
        CancellationToken cancellationToken, bool observeAccess = true, Func<CacheEntryMetadata, bool> compatible = null,
        bool waitForTransfer = true, Func<CacheEntryMetadata, Result<CacheReadOptions>> resolveAccess = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = this.Identity(key);
        options ??= new();
        if (!this.Enabled)
        {
            return Result<CacheLookup<ICacheReadHandle>>.Success(new(false, null, CacheLookupOutcome.Miss, CacheStorageOutcome.None));
        }

        if (options.RequireSeek && !this.Binding.Provider.Capabilities.SeekableRead)
        {
            return Result<CacheLookup<ICacheReadHandle>>.Failure(new CacheError(CacheErrorCode.Unsupported, "This provider does not expose arbitrary logical seeking."));
        }

        Result<CacheAdmissionLease> admission;
        if (this.CollectionResources.Transfers.TryAcquire(out var immediate))
        {
            admission = Result<CacheAdmissionLease>.Success(immediate);
        }
        else
        {
            // Under stream pressure, metadata misses/absence/changed versions still complete without
            // occupying or waiting for payload capacity. The normal hit path performs no preflight.
            var peek = await this.Access.CallAsync(token => this.Access.Provider.LookupAsync(identity,
                new(false, options.ExpectedVersion, ObserveAccess: false), token), cancellationToken).ConfigureAwait(false);
            if (peek.IsFailure) { return Result<CacheLookup<ICacheReadHandle>>.Failure(peek); }

            if (peek.Value.State == CacheReadValidationState.Changed)
            {
                return Result<CacheLookup<ICacheReadHandle>>.Failure(new CacheError(CacheErrorCode.Conflict, "The acquired cache version changed."));
            }

            if (peek.Value.State == CacheReadValidationState.Missing || (compatible is not null && !compatible(peek.Value.Metadata))) { return Miss(); }

            if (peek.Value.Metadata.IsNotFound)
            {
                return Result<CacheLookup<ICacheReadHandle>>.Success(new(false, null, CacheLookupOutcome.Hit, CacheStorageOutcome.None,
                    isNotFound: true, version: peek.Value.Metadata.Version));
            }

            admission = waitForTransfer ? await this.CollectionResources.Transfers.AcquireAsync(this.Binding.Options.Loading.WaitTimeout,
                cancellationToken).ConfigureAwait(false) : Result<CacheAdmissionLease>.Failure(new CacheError(CacheErrorCode.Capacity,
                "The ordered binary batch cannot retain more independent handles within its transfer bound."));
        }

        if (admission.IsFailure)
        {
            return Result<CacheLookup<ICacheReadHandle>>.Failure(admission);
        }

        var result = await this.Access.CallAsync(async token =>
        {
            var handedOff = false;
            CacheHotEntryLease pin = null;
            try
            {
                // Explicit seeking/ranges keep the native access path and its exact declared capabilities.
                pin = !options.RequireSeek && !options.Offset.HasValue && !options.Length.HasValue ? this.Hot?.TryAcquire(identity, false) : null;
                if (pin is not null) { this.Observations?.Record(CacheObservationKind.HotCandidate); }

                if (pin is not null && options.ExpectedVersion.HasValue && options.ExpectedVersion.Value != pin.Metadata.Version)
                {
                    pin.Dispose();
                    pin = null;
                }

                this.Observations?.Record(pin is null ? CacheObservationKind.ProviderPayloadRead : CacheObservationKind.ProviderValidation);
                var read = pin is null ? await this.Access.Provider.LookupAsync(identity,
                    new(true, options.ExpectedVersion, options.RequireSeek, observeAccess), token).ConfigureAwait(false) :
                    await this.Access.Provider.ValidateReadAsync(identity, pin.Metadata.Version,
                        new(ObserveAccess: observeAccess), token).ConfigureAwait(false);
                if (pin is not null && read.IsSuccess && (read.Value.State == CacheReadValidationState.Changed ||
                    (read.Value.State == CacheReadValidationState.Valid && !SameHotRepresentation(pin.Metadata, read.Value.Metadata))))
                {
                    this.Observations?.Record(CacheObservationKind.HotVersionChanged);
                    this.Hot.Retire(identity, pin.Metadata.Version);
                    pin.Dispose();
                    pin = null;
                    if (read.Value.Handle is not null) { await read.Value.Handle.DisposeAsync().ConfigureAwait(false); }

                    this.Observations?.Record(CacheObservationKind.ProviderPayloadRead);
                    read = await this.Access.Provider.LookupAsync(identity,
                        new(true, options.ExpectedVersion, options.RequireSeek, observeAccess), token).ConfigureAwait(false);
                }

                if (read.IsFailure)
                {
                    if (pin is not null) { this.Observations?.Record(CacheObservationKind.HotValidationFailure); }

                    return Result<CacheLookup<ICacheReadHandle>>.Failure(read);
                }

                if (read.Value.State == CacheReadValidationState.Changed)
                {
                    if (read.Value.Handle is not null)
                    {
                        await read.Value.Handle.DisposeAsync().ConfigureAwait(false);
                    }

                    return Result<CacheLookup<ICacheReadHandle>>.Failure(new CacheError(CacheErrorCode.Conflict, "The acquired cache version changed."));
                }

                if (read.Value.State == CacheReadValidationState.Missing)
                {
                    if (pin is not null) { this.Hot.Retire(identity, pin.Metadata.Version); }

                    return Miss();
                }

                var metadata = read.Value.Metadata;
                if (compatible is not null && !compatible(metadata))
                {
                    if (read.Value.Handle is not null)
                    {
                        await read.Value.Handle.DisposeAsync().ConfigureAwait(false);
                    }

                    return Miss(); // An older/newer deployment's registered representation remains untouched.
                }

                if (metadata.IsNotFound)
                {
                    return Result<CacheLookup<ICacheReadHandle>>.Success(new(false, null, CacheLookupOutcome.Hit,
                        CacheStorageOutcome.None, isNotFound: true, version: metadata.Version));
                }

                var acquired = read.Value.Handle;
                if (pin is not null)
                {
                    if (acquired is not null) { await acquired.DisposeAsync().ConfigureAwait(false); }

                    acquired = new CacheHotProviderReadHandle(pin, metadata);
                    this.Observations?.Record(CacheObservationKind.HotBinaryReuse);
                    if (observeAccess) { this.Hot.ObserveValidated(identity, metadata.Version); }

                    pin = null; // The independent memory snapshot now owns the pin.
                }

                if (acquired is null)
                {
                    return Result<CacheLookup<ICacheReadHandle>>.Failure(new CacheError(CacheErrorCode.Corruption, "A positive acquired entry has no payload."));
                }

                var effectiveAccess = resolveAccess?.Invoke(metadata) ?? Result<CacheReadOptions>.Success(options);
                if (effectiveAccess.IsFailure)
                {
                    await acquired.DisposeAsync().ConfigureAwait(false);
                    return Result<CacheLookup<ICacheReadHandle>>.Failure(effectiveAccess);
                }

                handedOff = true; // The reader factory owns every exit from this point, including failures.
                CacheHotRead capture = null;
                if (this.Hot is not null && acquired is not CacheHotProviderReadHandle && !effectiveAccess.Value.RequireSeek &&
                    !effectiveAccess.Value.Offset.HasValue && !effectiveAccess.Value.Length.HasValue)
                {
                    capture = CacheHotRead.TryCreate(acquired, this.Hot, this.Resources.Transient);
                    acquired = capture ?? acquired;
                }

                var opened = await CachePayloadReader.OpenAsync(acquired, effectiveAccess.Value, this.Binding, this.Resources,
                    this.Tasks, this.Time, admission.Value, token, this.Transforms,
                    this.Hot is null || !observeAccess ? null : this.CreateHotValidatedCallback(metadata, capture),
                    this.Binding.Provider.Options.Breaker.Enabled && this.Binding.Provider.Capabilities.Persistent ? this.Access.Provider as ICacheProviderInfrastructureObserver : null,
                    this.Observations).ConfigureAwait(false);
                if (opened.IsFailure)
                {
                    return opened.Errors.OfType<CacheError>().Any(e => e.Code == CacheErrorCode.Compatibility) ? Miss() :
                        Result<CacheLookup<ICacheReadHandle>>.Failure(opened);
                }

                if (opened.Value is CacheReadHandle handle)
                {
                    handle.Observe(CacheTransferObservationSession.Start(this.Binding.Name, this.Binding.Provider.Name,
                        handle.IntegrityScope != CacheReadIntegrityScope.FullEntry, this.Observations, this.Time, this.TransferObservers));
                }

                return Result<CacheLookup<ICacheReadHandle>>.Success(new(true, opened.Value, CacheLookupOutcome.Hit,
                    CacheStorageOutcome.None, version: metadata.Version));
            }
            finally
            {
                pin?.Dispose();
                if (!handedOff)
                {
                    admission.Value.Dispose();
                }
            }
        }, cancellationToken, async lookup =>
        {
            if (lookup.Value is not null)
            {
                await lookup.Value.DisposeAsync().ConfigureAwait(false);
            }
        }, notStarted: () =>
        {
            admission.Value.Dispose();
            return ValueTask.CompletedTask;
        }).ConfigureAwait(false);

        return result;
    }

    /// <summary>Performs a conditionally fenced exact-key removal and retires a confirmed matching local hot representation.</summary>
    public ValueTask<Result> RemoveAsync(string key, CacheWriteCondition condition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = this.Identity(key);
        var retained = this.Hot?.GetVersion(identity);
        return !this.Enabled ? ValueTask.FromResult(Result.Success()) : this.Access.CallAsync(async token =>
        {
            var result = await this.Access.Provider.RemoveAsync(identity, condition, token).ConfigureAwait(false);
            if (result.IsSuccess && retained.HasValue) { this.Hot.Retire(identity, retained.Value); }

            return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
        }, cancellationToken);
    }

    /// <summary>Renews only a currently valid authoritative entry within its original absolute expiry cap.</summary>
    public ValueTask<Result> TouchAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = this.Identity(key);
        return !this.Enabled ? ValueTask.FromResult(Result.Success()) : this.Access.CallAsync(async token =>
        {
            var result = await this.Access.Provider.TouchAsync(identity, cancellationToken: token).ConfigureAwait(false);
            return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
        }, cancellationToken);
    }

    /// <summary>Evaluates a bounded payload-free selection without changing entries or publication fences.</summary>
    public async ValueTask<Result<CacheInvalidationPreview>> PreviewAsync(CacheSelection selection, CancellationToken cancellationToken)
    {
        var validation = CacheSelectionValidator.Validate(selection, this.Binding.Options.Dimensions);
        if (validation.IsFailure)
        {
            return Result<CacheInvalidationPreview>.Failure(validation);
        }

        if (!this.Enabled || selection.IsEmpty)
        {
            return Result<CacheInvalidationPreview>.Success(new() { CountIsExact = true });
        }

        var limits = this.Binding.Options.Limits;
        var cutoff = this.Time.GetUtcNow();
        var started = this.Time.GetTimestamp();
        var candidates = 0;
        string continuation = null;
        CacheMetadataPage page;
        IReadOnlyList<CacheEntryMetadata> sample = [];
        do
        {
            var result = await this.MetadataCallAsync(token => this.Access.Provider.QueryAsync(new(this.Catalog.Options.Namespace,
                this.Binding.Name, selection, cutoff, limits.MetadataPageSize, continuation, limits.MaxQueryCandidates - candidates), token), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return Result<CacheInvalidationPreview>.Failure(result);
            }

            page = result.Value;
            if (sample.Count == 0)
            {
                sample = page.Entries;
            }

            candidates += page.CandidatesExamined;
            continuation = page.Continuation;
        }
        while (continuation is not null && candidates < limits.MaxQueryCandidates && this.Time.GetElapsedTime(started) < limits.MaxQueryWork);

        return Result<CacheInvalidationPreview>.Success(new() { Count = page.Count, CountIsExact = page.CountIsExact && page.Completed,
            Continuation = continuation, Sample = sample });
    }

    /// <summary>Invalidates a validated selection or an explicitly requested clear through bounded provider work.</summary>
    public async ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheSelection selection, bool clear,
        CancellationToken cancellationToken)
    {
        if (!clear)
        {
            var validation = CacheSelectionValidator.Validate(selection, this.Binding.Options.Dimensions);
            if (validation.IsFailure)
            {
                return Result<CacheInvalidationOutcome>.Failure(validation);
            }
        }

        if (!this.Enabled || (!clear && selection.IsEmpty))
        {
            return Result<CacheInvalidationOutcome>.Success(new() { Completed = true });
        }

        // A partial native result explicitly carries continuation; tracked continuation is a separate operation service.
        var cutoff = this.Time.GetUtcNow();
        var result = await this.MetadataCallAsync(token => this.Access.Provider.InvalidateAsync(new(this.Catalog.Options.Namespace,
            this.Binding.Name, selection, cutoff, clear), token), cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) { this.Hot?.RetireMatching(selection, this.Binding.Options.Dimensions, cutoff, clear); }

        return result;
    }

    /// <summary>Executes one admitted metadata operation within the collection query budget without starting source work.</summary>
    /// <typeparam name="T">The declared value or operation result type.</typeparam>
    public async ValueTask<Result<T>> MetadataCallAsync<T>(Func<CancellationToken, ValueTask<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        var admission = await this.CollectionResources.MetadataQueries.AcquireAsync(this.Binding.Options.Loading.WaitTimeout,
            cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result<T>.Failure(admission);
        }

        return await this.Access.CallAsync(async token =>
        {
            using var lease = admission.Value;
            return await operation(token).ConfigureAwait(false);
        }, cancellationToken, notStarted: () =>
        {
            admission.Value.Dispose();
            return ValueTask.CompletedTask;
        }).ConfigureAwait(false);
    }

    private static Result<CacheLookup<ICacheReadHandle>> Miss() =>
        Result<CacheLookup<ICacheReadHandle>>.Success(new(false, null, CacheLookupOutcome.Miss, CacheStorageOutcome.None));

    /// <summary>Reuses a pinned immutable decoded graph only after authoritative exact-version and representation compatibility validation.</summary>
    /// <typeparam name="T">The declared value or operation result type.</typeparam>
    public async ValueTask<Result<CacheLookup<T>>?> TryGetDecodedAsync<T>(string key, Func<CacheEntryMetadata, bool> compatible,
        CancellationToken cancellationToken, bool observeAccess = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = this.Identity(key);
        using var pin = this.Hot?.TryAcquire(identity, true);
        if (pin is null) { return null; }

        this.Observations?.Record(CacheObservationKind.HotCandidate);
        this.Observations?.Record(CacheObservationKind.ProviderValidation);
        var read = await this.Access.CallAsync(token => this.Access.Provider.ValidateReadAsync(identity, pin.Metadata.Version,
            new(ObserveAccess: observeAccess), token), cancellationToken, async value =>
            {
                if (value.Handle is not null) { await value.Handle.DisposeAsync().ConfigureAwait(false); }
            }).ConfigureAwait(false);
        if (read.IsFailure) { this.Observations?.Record(CacheObservationKind.HotValidationFailure); return Result<CacheLookup<T>>.Failure(read); }

        if (read.Value.Handle is not null) { await read.Value.Handle.DisposeAsync().ConfigureAwait(false); }

        if (read.Value.State == CacheReadValidationState.Missing)
        {
            this.Hot.Retire(identity, pin.Metadata.Version);
            return Result<CacheLookup<T>>.Success(new(false, default, CacheLookupOutcome.Miss, CacheStorageOutcome.None));
        }

        if (read.Value.State == CacheReadValidationState.Changed || !SameHotRepresentation(pin.Metadata, read.Value.Metadata) ||
            !compatible(read.Value.Metadata))
        {
            this.Observations?.Record(CacheObservationKind.HotVersionChanged);
            this.Hot.Retire(identity, pin.Metadata.Version);
            return null;
        }

        if (observeAccess) { this.Hot.ObserveValidated(identity, pin.Metadata.Version); }

        this.Observations?.Record(CacheObservationKind.HotDecodedReuse);
        return Result<CacheLookup<T>>.Success(new(true, (T)pin.Representation.Value, CacheLookupOutcome.Hit,
            CacheStorageOutcome.None, version: read.Value.Metadata.Version));
    }

    private Action CreateHotValidatedCallback(CacheEntryMetadata metadata, CacheHotRead capture) => () =>
    {
        this.Hot.Admission.Observe(metadata);
        capture?.Validated();
    };

    private static bool SameHotRepresentation(CacheEntryMetadata retained, CacheEntryMetadata acquired) => acquired is not null &&
        !acquired.IsNotFound && retained.Identity == acquired.Identity && retained.Version == acquired.Version &&
        retained.EnvelopeVersion == acquired.EnvelopeVersion && retained.CodecId == acquired.CodecId && retained.CodecVersion == acquired.CodecVersion;

    /// <summary>Stages and validates a bounded payload, then publishes only while captured source authority and write conditions remain valid.</summary>
    public async ValueTask<Result<CacheWriteOutcome>> WriteAsync(CacheItem item, CacheEntryDescriptor descriptor,
        CacheWriteCondition condition, string formatId, int formatVersion, Func<Stream, CancellationToken, ValueTask<Result>> encode,
        Func<Stream, CancellationToken, ValueTask<Result>> validator, CancellationToken cancellationToken,
        CachePublicationFence? capturedFence = null, CacheOwnerToken? owner = null, bool isNotFound = false, DateTimeOffset? deadline = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(item);
        var itemValidation = CacheSizePolicy.ValidateItem(item, this.Binding.Options);
        if (itemValidation.IsFailure) { return Result<CacheWriteOutcome>.Failure(itemValidation); }

        descriptor ??= new();
        var identity = this.Identity(item.Key);
        var retained = this.Hot?.GetVersion(identity);
        var dimensions = CacheSizePolicy.MergeDimensions(item, descriptor, this.Binding.Options);
        var declared = CacheSizePolicy.ValidateDescriptor(descriptor, this.Binding.Options);
        var lifetime = CacheExpirationPolicy.Create(this.Binding.Options, descriptor, isNotFound, this.Time.GetUtcNow());
        if (dimensions.IsFailure || declared.IsFailure || lifetime.IsFailure)
        {
            return Result<CacheWriteOutcome>.Failure().WithErrors(dimensions.IsFailure ? dimensions.Errors : declared.IsFailure ? declared.Errors : lifetime.Errors);
        }

        if (!this.Enabled || descriptor.SuppressStorage || (isNotFound && !this.Binding.Options.NegativeCaching.Enabled && !condition.ExpectedVersion.HasValue))
        {
            return Result<CacheWriteOutcome>.Success(new(CacheStorageOutcome.Suppressed));
        }

        var admitted = isNotFound ? Result<CacheAdmissionLease>.Success(null) : await this.CollectionResources.Transfers.AcquireAsync(
            this.Binding.Options.Loading.WaitTimeout, cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure)
        {
            return Result<CacheWriteOutcome>.Failure(admitted);
        }

        using var transfer = admitted.Value;
        CachePublicationFence fence;
        if (capturedFence.HasValue)
        {
            fence = capturedFence.Value;
        }
        else
        {
            var captured = await this.Access.CallAsync(token => this.Access.Provider.CaptureFenceAsync(identity, token), cancellationToken,
                async late => { await this.Access.Provider.ReleaseFenceAsync(identity, late).ConfigureAwait(false); }).ConfigureAwait(false);
            if (captured.IsFailure)
            {
                return Result<CacheWriteOutcome>.Failure(captured);
            }

            fence = captured.Value;
        }

        try
        {
            if (isNotFound && !this.Binding.Options.NegativeCaching.Enabled)
            {
                var removed = await this.Access.CallAsync(token => this.Access.Provider.RemoveAsync(new CacheProviderRemoval(identity,
                    condition, fence, owner, deadline, OnlyExpectedVersion: true), token), cancellationToken).ConfigureAwait(false);
                if (removed.IsSuccess && retained.HasValue) { this.Hot.Retire(identity, retained.Value); }

                return removed.IsSuccess ? Result<CacheWriteOutcome>.Success(new(CacheStorageOutcome.Suppressed)) : Result<CacheWriteOutcome>.Failure(removed);
            }

            var request = new CacheProviderWriteRequest(identity, fence, condition,
                isNotFound ? 1 : CacheSizePolicy.MaximumStoredBytes(this.Binding.Options), owner, Deadline: deadline);
            if (isNotFound)
            {
                var staged = await CacheWriteSessionAccess.BeginAsync(this.Access, request, this.Binding,
                    this.CollectionResources, this.Tasks, cancellationToken).ConfigureAwait(false);
                if (staged.IsFailure)
                {
                    return Result<CacheWriteOutcome>.Failure(staged);
                }

                await using var marker = staged.Value;
                var published = await marker.PublishAsync(new()
                {
                    Identity = identity, DisplayKey = item.Key, ContractId = this.Binding.Contract.Alias, Version = marker.Version,
                    IsNotFound = true, Dimensions = dimensions.Value, PublishedAt = lifetime.Value.PublishedAt,
                    ExpiresAt = lifetime.Value.ExpiresAt, AbsoluteExpiresAt = lifetime.Value.AbsoluteExpiresAt,
                    LastAccessAt = lifetime.Value.PublishedAt, Priority = CacheEvictionPriority.Low,
                    CodecId = formatId, CodecVersion = formatVersion, Compression = "none"
                }, cancellationToken).ConfigureAwait(false);
                if (published.IsSuccess && published.Value.StorageOutcome == CacheStorageOutcome.Stored) { this.Observations?.Record(CacheObservationKind.WriteSuccess); }

                if (published.IsSuccess && published.Value.StorageOutcome == CacheStorageOutcome.Stored && retained.HasValue)
                {
                    this.Hot.Retire(identity, retained.Value);
                }

                return published;
            }

            var created = await CachePayloadStage.CreateAsync(this.Access.Provider, request, this.Binding, this.Resources, this.Time,
                item, formatId, formatVersion, cancellationToken, this.Access, this.Tasks, this.PayloadPipeline, this.Transforms).ConfigureAwait(false);
            if (created.IsFailure)
            {
                return Result<CacheWriteOutcome>.Failure(created);
            }

            await using var stage = created.Value;
            var encoded = await encode(stage.Stream, cancellationToken).ConfigureAwait(false);
            if (encoded.IsFailure)
            {
                return Result<CacheWriteOutcome>.Failure(encoded);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var completed = await stage.CompleteAsync(descriptor, validator, cancellationToken).ConfigureAwait(false);
            if (completed.IsFailure)
            {
                return Result<CacheWriteOutcome>.Failure(completed);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var stored = await stage.PublishAsync(cancellationToken).ConfigureAwait(false);
            if (stored.IsSuccess && stored.Value.StorageOutcome == CacheStorageOutcome.Stored) { this.Observations?.Record(CacheObservationKind.WriteSuccess); }

            if (stored.IsSuccess && stored.Value.StorageOutcome == CacheStorageOutcome.Stored && retained.HasValue)
            {
                this.Hot.Retire(identity, retained.Value);
            }

            return stored;
        }
        catch (CacheTransferException exception)
        {
            return Result<CacheWriteOutcome>.Failure(new CacheError(exception.Code, "The bounded payload write could not complete."));
        }
        catch (StreamSizeLimitExceededException)
        {
            return Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.Capacity, "The payload exceeds its finite byte limit."));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            return Result<CacheWriteOutcome>.Failure(new CacheError(exception is InvalidDataException ? CacheErrorCode.Corruption :
                CacheErrorCode.ProviderUnavailable, "The payload write did not complete."));
        }
        finally
        {
            if (!capturedFence.HasValue)
            {
                await this.Access.CallAsync(token => this.Access.Provider.ReleaseFenceAsync(identity, fence, token), CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Copies caller-owned input with admitted bounded buffers, preserving cancellation and actual transfer accounting.</summary>
    public async ValueTask<Result> CopyInputAsync(Stream input, Stream destination, CancellationToken cancellationToken)
    {
        var size = this.Binding.Options.Streaming.BufferSize;
        if (!ResourceCharge.TryReserve(size, out var memory, this.Resources.Transient, this.CollectionResources.Resident))
        {
            return Result.Failure(new CacheError(CacheErrorCode.Capacity, "Input transfer buffer admission was rejected."));
        }

        using (memory)
        {
            var buffer = GC.AllocateUninitializedArray<byte>(size);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (read == 0)
                {
                    return Result.Success();
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Bounds the caller wait while tracking actual unfinished work and retaining its admitted resource leases after timeout or cancellation.</summary>
    /// <typeparam name="T">The declared value or operation result type.</typeparam>
    public async ValueTask<Result<T>> WaitOwnedAsync<T>(Task<Result<T>> actual, CancellationToken cancellationToken,
        TimeSpan timeout, CacheErrorCode timeoutCode)
    {
        try
        {
            return await actual.WaitAsync(timeout, this.Time, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            this.Tasks.Track(actual, "owned-tail");
            return Result<T>.Failure(new CacheError(timeoutCode, "The caller's bounded cache wait expired."));
        }
        catch (OperationCanceledException)
        {
            this.Tasks.Track(actual, "owned-tail");
            throw;
        }
    }
}