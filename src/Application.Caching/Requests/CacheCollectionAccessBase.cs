// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Implements metadata-only management using the same initialization, provider admission, and immutable contract as ordinary access.</summary>
/// <example><code>var metadata = await access.ReadMetadataAsync("product:42", cancellationToken);</code></example>
public abstract class CacheCollectionAccessBase : ICacheCollectionAccess
{
    private readonly CacheCatalog catalog;
    private readonly CacheRuntimeResources resources;
    private readonly CacheProviderAccess access;
    private readonly TimeProvider time;

    /// <summary>Creates a declared adapter without creating additional hot stores or invoking native readiness.</summary>
    protected CacheCollectionAccessBase(CacheCollectionBinding binding, CacheCatalog catalog, CacheRuntimeResources resources,
        CacheProviderRegistry providers, CacheRuntimeTasks tasks, TimeProvider time, IServiceProvider services)
    {
        this.Binding = binding;
        this.catalog = catalog;
        this.resources = resources;
        this.time = time;
        this.access = new(binding, new CacheDeferredStoreProvider(binding.Provider, () => providers.Get(binding.Provider.Name)),
            resources, services.GetRequiredService<CacheProviderInitialization>(), tasks, time);
    }

    /// <inheritdoc />
    public CacheCollectionBinding Binding { get; }

    /// <inheritdoc />
    public abstract bool SupportsStreamWrites { get; }

    /// <inheritdoc />
    public async ValueTask<Result<CacheLookup<CacheEntryMetadata>>> ReadMetadataAsync(string key, CancellationToken cancellationToken = default)
    {
        var identity = new CacheEntryIdentity(this.catalog.Options.Namespace, this.Binding.Name, this.Binding.Options.ContractVersion, key);
        if (!this.catalog.Options.Enabled || !this.Binding.Options.Enabled)
        {
            return Result<CacheLookup<CacheEntryMetadata>>.Success(new(false, null, CacheLookupOutcome.Miss, CacheStorageOutcome.None));
        }

        var result = await this.access.CallAsync(token => this.access.Provider.LookupAsync(identity, new(ObserveAccess: true), token),
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) { return Result<CacheLookup<CacheEntryMetadata>>.Failure(result); }

        if (result.Value.Handle is not null) { await result.Value.Handle.DisposeAsync().ConfigureAwait(false); }

        var metadata = result.Value.Metadata;
        var found = result.Value.State == CacheReadValidationState.Valid && metadata is not null && this.Compatible(metadata);
        return Result<CacheLookup<CacheEntryMetadata>>.Success(new(found && !metadata.IsNotFound, found ? metadata : null,
            found ? CacheLookupOutcome.Hit : CacheLookupOutcome.Miss, CacheStorageOutcome.None,
            isNotFound: found && metadata.IsNotFound, version: found ? metadata.Version : null));
    }

    /// <inheritdoc />
    public async ValueTask<Result<CacheMetadataPage>> QueryAsync(CacheSelection selection, int take, string continuation, CancellationToken cancellationToken = default,
        DateTimeOffset? cutoff = null)
    {
        if (!this.catalog.Options.Enabled || !this.Binding.Options.Enabled)
        {
            return Result<CacheMetadataPage>.Failure(new CacheError(CacheErrorCode.Unsupported, "Metadata browsing is unavailable while this collection is disabled."));
        }

        var limits = this.Binding.Options.Limits;
        if (take < 1 || take > limits.MetadataPageSize)
        {
            return Result<CacheMetadataPage>.Failure(new CacheError(CacheErrorCode.Validation, "The metadata page exceeds its declared bound."));
        }

        var valid = CacheSelectionValidator.Validate(selection, this.Binding.Options.Dimensions);
        if (valid.IsFailure) { return Result<CacheMetadataPage>.Failure(valid); }

        var admitted = await this.resources.ForCollection(this.Binding.Name).MetadataQueries.AcquireAsync(
            this.Binding.Options.Loading.WaitTimeout, cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure) { return Result<CacheMetadataPage>.Failure(admitted); }

        using var lease = admitted.Value;
        return await this.access.CallAsync(token => this.access.Provider.QueryAsync(new(this.catalog.Options.Namespace,
            this.Binding.Name, selection, cutoff ?? this.time.GetUtcNow(), take, continuation, limits.MaxQueryCandidates), token), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public abstract ValueTask<Result<CacheLookup<ICacheReadHandle>>> OpenReadAsync(string key, CacheReadOptions options, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract ValueTask<Result<CacheWriteOutcome>> SetStreamAsync(CacheItem item, Stream input, CacheEntryDescriptor descriptor, CacheWriteCondition condition, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract ValueTask<Result<CacheWriteOutcome>> SetValueAsync(CacheItem item, object value, CacheEntryDescriptor descriptor, CacheWriteCondition condition, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract ValueTask<Result> RemoveAsync(string key, CacheWriteCondition condition, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract ValueTask<Result<CacheInvalidationPreview>> PreviewAsync(CacheSelection selection, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheSelection selection, bool clear, CancellationToken cancellationToken = default);

    /// <summary>Tests the metadata format against the exact startup-registered readers without opening bytes.</summary>
    protected abstract bool Compatible(CacheEntryMetadata metadata);
}