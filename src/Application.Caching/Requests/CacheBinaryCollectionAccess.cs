// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Adapts one declared binary collection without decoding or buffering request bodies.</summary>
/// <example><code>var adapter = registry.Get("reports");</code></example>
public sealed class CacheBinaryCollectionAccess : CacheCollectionAccessBase
{
    private readonly ICacheBinaryCollection collection;
    private readonly CacheBinaryValueContract contract;

    /// <summary>Resolves the ordinary binary handle without provider I/O.</summary>
    public CacheBinaryCollectionAccess(CacheCollectionBinding binding, IApplicationCache cache, CacheCatalog catalog, CacheRuntimeResources resources,
        CacheProviderRegistry providers, CacheRuntimeTasks tasks, TimeProvider time, IServiceProvider services)
        : base(binding, catalog, resources, providers, tasks, time, services)
    {
        this.collection = cache.GetBinaryCollection(binding.Name);
        this.contract = (CacheBinaryValueContract)binding.Contract;
    }

    /// <inheritdoc />
    public override bool SupportsStreamWrites => true;

    /// <inheritdoc />
    public override ValueTask<Result<CacheLookup<ICacheReadHandle>>> OpenReadAsync(string key, CacheReadOptions options, CancellationToken cancellationToken = default) =>
        this.collection.OpenReadAsync(key, options, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheWriteOutcome>> SetStreamAsync(CacheItem item, Stream input, CacheEntryDescriptor descriptor,
        CacheWriteCondition condition, CancellationToken cancellationToken = default) => this.collection.SetAsync(item, input, descriptor, condition, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheWriteOutcome>> SetValueAsync(CacheItem item, object value, CacheEntryDescriptor descriptor,
        CacheWriteCondition condition, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.Unsupported, "A binary contract requires streamed input.")));

    /// <inheritdoc />
    public override ValueTask<Result> RemoveAsync(string key, CacheWriteCondition condition, CancellationToken cancellationToken = default) =>
        this.collection.RemoveAsync(key, condition, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheInvalidationPreview>> PreviewAsync(CacheSelection selection, CancellationToken cancellationToken = default) =>
        this.collection.PreviewInvalidationAsync(selection, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheSelection selection, bool clear, CancellationToken cancellationToken = default) =>
        clear ? this.collection.ClearAsync(cancellationToken) : this.collection.InvalidateAsync(selection, cancellationToken);

    /// <inheritdoc />
    protected override bool Compatible(CacheEntryMetadata metadata) => metadata.IsNotFound ||
        (metadata.EnvelopeVersion is 1 or 2 && metadata.CodecId == this.contract.FormatId && metadata.CodecVersion == this.contract.FormatVersion);
}