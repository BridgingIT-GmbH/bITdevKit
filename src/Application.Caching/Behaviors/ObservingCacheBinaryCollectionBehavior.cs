// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records logical public outcomes once without changing mandatory cache results or creating warm-hit contexts.</summary>
/// <param name="inner">The once-composed mandatory collection and optional behavior chain.</param>
/// <param name="observations">The startup-declared intrinsic observation slot.</param>
/// <param name="clock">The monotonic observation clock.</param>
/// <example><code>var observed = new ObservingCacheBinaryCollectionBehavior(collection, slot, clock);</code></example>
public sealed class ObservingCacheBinaryCollectionBehavior(ICacheBinaryCollection inner, CacheObservationSlot observations, TimeProvider clock) : ICacheBinaryCollection
{
    /// <inheritdoc />
    public string Name => inner.Name;

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> OpenReadAsync(string key, CacheReadOptions options = null, CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Lookup(CacheOperationObservation.Begin(observations, clock, key, CacheOperationKind.OpenRead), inner.OpenReadAsync(key, options, cancellationToken), observations, clock, CacheOperationKind.OpenRead);

    /// <inheritdoc />
    public ValueTask<Result<CacheWriteOutcome>> SetAsync(CacheItem item, Stream input, CacheEntryDescriptor descriptor = null,
        CacheWriteCondition condition = default, CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Write(CacheOperationObservation.Begin(observations, clock, item?.Key, CacheOperationKind.Set), inner.SetAsync(item, input, descriptor, condition, cancellationToken), observations, clock);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> GetOrLoadAsync(CacheItem item, CacheBinaryLoadDelegate loader, CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Lookup(CacheOperationObservation.Begin(observations, clock, item?.Key, CacheOperationKind.GetOrLoad), inner.GetOrLoadAsync(item, loader, cancellationToken), observations, clock, CacheOperationKind.GetOrLoad);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> GetOrLoadAsync(CacheItem item, CacheBinaryLoadDelegate loader,
        CacheAccessOptions options, CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Lookup(CacheOperationObservation.Begin(observations, clock, item?.Key, CacheOperationKind.GetOrLoad), inner.GetOrLoadAsync(item, loader, options, cancellationToken), observations, clock, CacheOperationKind.GetOrLoad);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> RefreshAsync(CacheItem item, CacheBinaryLoadDelegate loader, CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Lookup(CacheOperationObservation.Begin(observations, clock, item?.Key, CacheOperationKind.Refresh), inner.RefreshAsync(item, loader, cancellationToken), observations, clock, CacheOperationKind.Refresh);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> RefreshAsync(CacheItem item, CacheBinaryLoadDelegate loader, CacheAccessOptions options, CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Lookup(CacheOperationObservation.Begin(observations, clock, item?.Key, CacheOperationKind.Refresh), inner.RefreshAsync(item, loader, options, cancellationToken), observations, clock, CacheOperationKind.Refresh);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>>> GetOrLoadManyAsync(
        IReadOnlyList<CacheItem> items, CacheBinaryLoadDelegate loader, CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Batch(CacheOperationObservation.Begin(observations, clock, null, CacheOperationKind.GetOrLoadMany), inner.GetOrLoadManyAsync(items, loader, cancellationToken), observations, clock, CacheOperationKind.GetOrLoadMany);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>>> GetOrLoadManyAsync(
        IReadOnlyList<CacheItem> items, CacheBinaryBatchLoadDelegate loader, CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Batch(CacheOperationObservation.Begin(observations, clock, null, CacheOperationKind.GetOrLoadMany), inner.GetOrLoadManyAsync(items, loader, cancellationToken), observations, clock, CacheOperationKind.GetOrLoadMany);

    /// <inheritdoc />
    public ValueTask<Result> RemoveAsync(string key, CacheWriteCondition condition = default, CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Action(CacheOperationObservation.Begin(observations, clock, key, CacheOperationKind.Remove), inner.RemoveAsync(key, condition, cancellationToken), observations, clock);

    /// <inheritdoc />
    public ValueTask<Result> TouchAsync(string key, CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Action(CacheOperationObservation.Begin(observations, clock, key, CacheOperationKind.Touch), inner.TouchAsync(key, cancellationToken), observations, clock);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationPreview>> PreviewInvalidationAsync(CacheSelection selection, CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Plain(CacheOperationObservation.Begin(observations, clock, null, CacheOperationKind.PreviewInvalidation), inner.PreviewInvalidationAsync(selection, cancellationToken), observations, clock);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheSelection selection, CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Invalidation(CacheOperationObservation.Begin(observations, clock, null, CacheOperationKind.Invalidate), inner.InvalidateAsync(selection, cancellationToken), observations, clock);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationOutcome>> ClearAsync(CancellationToken cancellationToken = default) =>
        CacheOperationObservation.Invalidation(CacheOperationObservation.Begin(observations, clock, null, CacheOperationKind.Clear), inner.ClearAsync(cancellationToken), observations, clock);
}