// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Reads and loads raw binary payloads through one declared, bounded collection.</summary>
/// <remarks>
/// Source loads return handles only after storage publication is confirmed. A successful empty payload is distinct from confirmed source absence.
/// Each acquired handle has its own stream position and bounded lifetime. The caller must dispose it asynchronously and inspect terminal read completion.
/// </remarks>
/// <example><code>var result = await collection.OpenReadAsync("report:42", cancellationToken: cancellationToken);</code></example>
public interface ICacheBinaryCollection
{
    /// <summary>Gets the canonical declared name.</summary>
    /// <value>The startup-declared collection name, using invariant case normalization.</value>
    /// <example><code>var name = collection.Name;</code></example>
    string Name { get; }

    /// <summary>Acquires a valid immutable snapshot without invoking a source.</summary>
    /// <param name="key">The exact key using invariant case equivalence. Wildcards have no special meaning.</param>
    /// <param name="options">An optional expected version, seek requirement, or logical-byte range. Null requests a full forward read.</param>
    /// <param name="cancellationToken">Cancels this acquisition or this caller's bounded wait.</param>
    /// <returns>A caller-owned handle, confirmed absence, a successful miss, or an explicit failure.</returns>
    /// <remarks>
    /// Unsupported seek or range requests fail before payload delivery. A handle exposes logical bytes with cache envelopes and compression removed.
    /// Copying the stream is separate from integrity validation. Inspect <see cref="ICacheReadHandle.WaitForCompletionAsync" /> after consumption.
    /// </remarks>
    /// <example><code>
    /// var result = await collection.OpenReadAsync("report:42", cancellationToken: cancellationToken);
    /// if (result.IsSuccess &amp;&amp; result.Value.Found)
    /// {
    ///     await using var handle = result.Value.Value;
    ///     await handle.Stream.CopyToAsync(destination, cancellationToken);
    ///     var completion = await handle.WaitForCompletionAsync(cancellationToken);
    /// }
    /// </code></example>
    ValueTask<Result<CacheLookup<ICacheReadHandle>>> OpenReadAsync(string key, CacheReadOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>Stages, validates, and atomically publishes caller-owned binary input.</summary>
    /// <param name="item">The exact key and known dependencies captured for the new entry.</param>
    /// <param name="input">The caller-owned source, read from its current position. The cache neither retains nor disposes it.</param>
    /// <param name="descriptor">Additional dependencies and bounded metadata overrides. Null uses collection defaults.</param>
    /// <param name="condition">The expected-version or create-only condition, checked atomically at publication. Default is unconditional.</param>
    /// <param name="cancellationToken">Cancels the transfer or this caller's bounded wait.</param>
    /// <returns>The actual storage acknowledgement and a version only after confirmed publication, or an explicit failure.</returns>
    /// <remarks>
    /// Zero bytes represent a successful empty payload. Binary storage suppression is unsupported.
    /// Failed conditions preserve the current entry and never retry unconditionally. Publication still requires size, integrity, and invalidation-fence checks.
    /// </remarks>
    /// <example><code>var stored = await collection.SetAsync(new("report:42"), input, condition: new(CreateOnly: true), cancellationToken: cancellationToken);</code></example>
    ValueTask<Result<CacheWriteOutcome>> SetAsync(CacheItem item, Stream input, CacheEntryDescriptor descriptor = null,
        CacheWriteCondition condition = default, CancellationToken cancellationToken = default);

    /// <summary>Reads an existing entry or loads and publishes its source before opening an independent handle.</summary>
    /// <param name="item">The exact key and known dependencies captured for source work.</param>
    /// <param name="loader">The repeatable source writer using cache-owned services and a bounded destination that it must leave open.</param>
    /// <param name="cancellationToken">Cancels this caller's wait independently of other interested callers.</param>
    /// <returns>A caller-owned handle, confirmed absence, or an explicit source, storage, provider, or admission failure.</returns>
    /// <remarks>
    /// The configured ownership strategy may coalesce source work. Each successful caller opens its own stored snapshot rather than sharing a consumable stream.
    /// Unconfirmed publication cannot return a source payload handle. Loaders may be retried and must not perform business mutations.
    /// </remarks>
    /// <example><code>var result = await collection.GetOrLoadAsync(new("report:42"), loader, cancellationToken);</code></example>
    ValueTask<Result<CacheLookup<ICacheReadHandle>>> GetOrLoadAsync(CacheItem item, CacheBinaryLoadDelegate loader, CancellationToken cancellationToken = default);

    /// <summary>Reads or loads binary content with explicit access options.</summary>
    /// <param name="item">The exact key and known dependencies captured for source work.</param>
    /// <param name="loader">The repeatable source writer, which must leave the engine-owned destination open.</param>
    /// <param name="options">Normal access and optional background classification. Null uses normal foreground access.</param>
    /// <param name="cancellationToken">Cancels this caller's wait independently of other interested callers.</param>
    /// <returns>An independently owned stored handle, confirmed absence, or an explicit failure.</returns>
    /// <remarks>Binary bypass and loading on a disabled collection are unsupported and fail before source execution. Background access avoids foreground activity updates.</remarks>
    /// <example><code>var result = await collection.GetOrLoadAsync(item, loader, new(Background: true), cancellationToken);</code></example>
    ValueTask<Result<CacheLookup<ICacheReadHandle>>> GetOrLoadAsync(CacheItem item, CacheBinaryLoadDelegate loader,
        CacheAccessOptions options, CancellationToken cancellationToken = default);

    /// <summary>Loads fresh source content while preserving an independently valid old version.</summary>
    /// <param name="item">The exact key and captured dependencies for source work.</param>
    /// <param name="loader">The repeatable source writer using cache-owned services and a bounded destination.</param>
    /// <param name="cancellationToken">Cancels this caller's wait independently of other interested callers.</param>
    /// <returns>A caller-owned handle after confirmed publication, confirmed absence, or an explicit failure.</returns>
    /// <remarks>Publication remains conditional on the observed version and invalidation fences. Source or storage failure does not remove a valid old entry.</remarks>
    /// <example><code>var result = await collection.RefreshAsync(item, loader, cancellationToken);</code></example>
    ValueTask<Result<CacheLookup<ICacheReadHandle>>> RefreshAsync(CacheItem item, CacheBinaryLoadDelegate loader, CancellationToken cancellationToken = default);

    /// <summary>Refreshes with an optional expected version and background classification.</summary>
    /// <param name="item">The stable source key and captured dimensions.</param>
    /// <param name="loader">The repeatable source loader executed in an owned scope.</param>
    /// <param name="options">Normal access, background classification, and an optional acquired expected version. Null uses normal foreground access.</param>
    /// <param name="cancellationToken">This caller's wait cancellation token.</param>
    /// <returns>An independently owned stored handle, confirmed absence, or an explicit failure. A changed selected version starts no source work.</returns>
    /// <remarks>Binary bypass and disabled loading are unsupported. Background work avoids foreground activity and rechecks selected-version eligibility before source execution.</remarks>
    /// <example><code>await collection.RefreshAsync(item, loader, new(Background: true, ExpectedVersion: version), cancellationToken);</code></example>
    ValueTask<Result<CacheLookup<ICacheReadHandle>>> RefreshAsync(CacheItem item, CacheBinaryLoadDelegate loader, CacheAccessOptions options, CancellationToken cancellationToken = default);

    /// <summary>Reads or loads an ordered binary batch using a repeatable single-item writer.</summary>
    /// <param name="items">The bounded batch, without null members or equivalent duplicate keys. The engine captures it before starting work.</param>
    /// <param name="loader">The repeatable source writer for each owned miss. It must leave each engine-owned destination open.</param>
    /// <param name="cancellationToken">Cancels this caller's batch wait.</param>
    /// <returns>An outer admission result containing independent per-item results in input order.</returns>
    /// <remarks>Outer success permits partial item failures. Dispose every successful item handle. Consumable streams are never shared between callers.</remarks>
    /// <example><code>var result = await collection.GetOrLoadManyAsync([new("report:42"), new("report:43")], loader, cancellationToken);</code></example>
    ValueTask<Result<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>>> GetOrLoadManyAsync(
        IReadOnlyList<CacheItem> items, CacheBinaryLoadDelegate loader, CancellationToken cancellationToken = default);

    /// <summary>Loads owned binary misses through bounded batch staging and returns ordered partial results.</summary>
    /// <param name="items">The bounded batch, without null members or equivalent duplicate keys.</param>
    /// <param name="loader">The repeatable batch source, which acquires cache-owned destinations only for its owned keys and leaves them open.</param>
    /// <param name="cancellationToken">Cancels this caller's batch wait independently of other interested callers.</param>
    /// <returns>An outer admission result containing ordered per-item stored handles, confirmed absence, or explicit failures.</returns>
    /// <remarks>
    /// Hits and foreign in-flight loads are excluded from the source context. Each yielded completion publishes independently before foreign work is joined.
    /// Omitted keys receive incomplete failures. Unknown or duplicate completion keys are contract failures. Dispose every successful handle.
    /// </remarks>
    /// <example><code>var result = await collection.GetOrLoadManyAsync(items, batchLoader, cancellationToken);</code></example>
    ValueTask<Result<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>>> GetOrLoadManyAsync(
        IReadOnlyList<CacheItem> items, CacheBinaryBatchLoadDelegate loader, CancellationToken cancellationToken = default);

    /// <summary>Removes an exact key and fences prior source publication when its condition succeeds.</summary>
    /// <param name="key">The exact key to remove, with no wildcard interpretation.</param>
    /// <param name="condition">An optional atomic expected-version condition. Default removes unconditionally.</param>
    /// <param name="cancellationToken">Cancels the provider mutation or wait.</param>
    /// <returns>Success when the removal boundary is established, or an explicit failure such as a version conflict.</returns>
    /// <remarks>Unconditional removal fences old loads even for a missing key. A failed condition preserves the entry and its fences. Acquired read handles retain their immutable snapshots.</remarks>
    /// <example><code>var removed = await collection.RemoveAsync("report:42", new(handle.Metadata.Version), cancellationToken);</code></example>
    ValueTask<Result> RemoveAsync(string key, CacheWriteCondition condition = default, CancellationToken cancellationToken = default);

    /// <summary>Renews an eligible sliding entry without loading its source.</summary>
    /// <param name="key">The exact key whose authoritative validity is checked.</param>
    /// <param name="cancellationToken">Cancels the provider operation or wait.</param>
    /// <returns>The provider's renewal result, or an explicit failure.</returns>
    /// <remarks>Renewal cannot revive expired or invalidated content, extend the original absolute expiry, or reset source age.</remarks>
    /// <example><code>var touched = await collection.TouchAsync("report:42", cancellationToken);</code></example>
    ValueTask<Result> TouchAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Previews matching metadata within the collection's query limits.</summary>
    /// <param name="selection">The validated key or declared-dimension selection. An empty selection matches nothing.</param>
    /// <param name="cancellationToken">Cancels metadata enumeration or the wait.</param>
    /// <returns>The count, count accuracy, bounded sample, and any continuation, or an explicit failure.</returns>
    /// <remarks>Preview opens no payloads and changes neither entries nor fences. It is an observation, not a reservation for a later invalidation.</remarks>
    /// <example><code>var preview = await collection.PreviewInvalidationAsync(CacheSelection.Pattern("report:*"), cancellationToken);</code></example>
    ValueTask<Result<CacheInvalidationPreview>> PreviewInvalidationAsync(CacheSelection selection, CancellationToken cancellationToken = default);

    /// <summary>Invalidates matching entries and fences matching active loads through bounded provider work.</summary>
    /// <param name="selection">The validated key or declared-dimension selection. An empty selection matches nothing.</param>
    /// <param name="cancellationToken">Cancels provider invalidation or the wait.</param>
    /// <returns>The actual invalidation progress and continuation, or an explicit failure.</returns>
    /// <remarks>A successful result can be partial. Check <see cref="CacheInvalidationOutcome.Completed" /> before assuming every match was invalidated. Already acquired handles remain independent snapshots.</remarks>
    /// <example><code>var invalidated = await collection.InvalidateAsync(CacheSelection.Pattern("report:*"), cancellationToken);</code></example>
    ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheSelection selection, CancellationToken cancellationToken = default);

    /// <summary>Explicitly invalidates the whole logical collection across retained contract versions.</summary>
    /// <param name="cancellationToken">Cancels provider invalidation or the wait.</param>
    /// <returns>The actual invalidation progress and continuation, or an explicit failure.</returns>
    /// <remarks>Clear fences prior active loads and uses the same bounded work rules as selection invalidation. Inspect completion before assuming the whole collection is invalidated.</remarks>
    /// <example><code>var cleared = await collection.ClearAsync(cancellationToken);</code></example>
    ValueTask<Result<CacheInvalidationOutcome>> ClearAsync(CancellationToken cancellationToken = default);
}