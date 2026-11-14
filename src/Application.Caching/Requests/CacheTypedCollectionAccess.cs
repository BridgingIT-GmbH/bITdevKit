// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Adapts one trusted typed contract to management and boundary-mapped publication.</summary>
/// <typeparam name="T">The code-registered value type.</typeparam>
/// <example><code>var adapter = registry.Get("cards");</code></example>
public sealed class CacheTypedCollectionAccess<T> : CacheCollectionAccessBase
{
    private readonly ICacheCollection<T> collection;
    private readonly CacheResolvedCodecs<T> codecs;

    /// <summary>Resolves the ordinary collection handle and approved readers once without provider I/O.</summary>
    public CacheTypedCollectionAccess(CacheCollectionBinding binding, IApplicationCache cache, CacheCatalog catalog, CacheRuntimeResources resources,
        CacheProviderRegistry providers, CacheRuntimeTasks tasks, TimeProvider time, IServiceProvider services)
        : base(binding, catalog, resources, providers, tasks, time, services)
    {
        this.collection = cache.GetCollection<T>(binding.Name);
        this.codecs = ((CacheValueContract<T>)binding.Contract).Resolve(binding.Options.Codec, services);
    }

    /// <inheritdoc />
    public override bool SupportsStreamWrites => this.codecs.Validator is not null;

    /// <inheritdoc />
    public override ValueTask<Result<CacheLookup<ICacheReadHandle>>> OpenReadAsync(string key, CacheReadOptions options, CancellationToken cancellationToken = default) =>
        this.collection.OpenReadAsync(key, options, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheWriteOutcome>> SetStreamAsync(CacheItem item, Stream input, CacheEntryDescriptor descriptor,
        CacheWriteCondition condition, CancellationToken cancellationToken = default) => this.collection.SetEncodedAsync(item, input, descriptor, condition, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheWriteOutcome>> SetValueAsync(CacheItem item, object value, CacheEntryDescriptor descriptor,
        CacheWriteCondition condition, CancellationToken cancellationToken = default) => value is T || (value is null && default(T) is null) ?
        this.collection.SetAsync(item, (T)value, condition, descriptor, cancellationToken) :
        ValueTask.FromResult(Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.Contract, "The mapped value does not match this declared contract.")));

    /// <inheritdoc />
    public override ValueTask<Result> RemoveAsync(string key, CacheWriteCondition condition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (condition.CreateOnly)
        {
            return ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.Validation,
                "The removal condition does not identify a permitted exact mutation.")));
        }

        return condition.ExpectedVersion.HasValue ? this.collection.RemoveAsync(key, condition.ExpectedVersion.Value, cancellationToken) :
            this.collection.RemoveAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask<Result<CacheInvalidationPreview>> PreviewAsync(CacheSelection selection, CancellationToken cancellationToken = default) =>
        this.collection.PreviewInvalidationAsync(selection, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheSelection selection, bool clear, CancellationToken cancellationToken = default) =>
        clear ? this.collection.ClearAsync(cancellationToken) : this.collection.InvalidateAsync(selection, cancellationToken);

    /// <inheritdoc />
    protected override bool Compatible(CacheEntryMetadata metadata) => metadata.IsNotFound ||
        (metadata.EnvelopeVersion is 1 or 2 && this.codecs.Readers.TryGet<T>(new(metadata.CodecId, metadata.CodecVersion), out _));
}