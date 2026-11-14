// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Reads and loads derived values through one declared, bounded typed collection.</summary>
/// <typeparam name="T">The collection's one declared value contract.</typeparam>
/// <remarks>
/// Values are read-only and may be shared between callers without cloning. Copy a value before changing it.
/// A successful lookup distinguishes a value, including a permitted null or empty value, from confirmed source absence and a cache miss.
/// Source success and storage success are separate. Check <see cref="CacheLookup{T}.StorageOutcome" /> before assuming a loaded value was retained.
/// </remarks>
/// <example><code>var result = await collection.GetOrLoadAsync(new("product:42"), loader, cancellationToken);</code></example>
public interface ICacheCollection<T>
{
    /// <summary>Gets the canonical declared name.</summary>
    /// <value>The startup-declared collection name, using invariant case normalization.</value>
    /// <example><code>var name = collection.Name;</code></example>
    string Name { get; }

    /// <summary>Reads a valid cached value or confirmed absence without invoking a source.</summary>
    /// <param name="key">The exact key, compared using invariant case equivalence. Wildcards have no special meaning.</param>
    /// <param name="cancellationToken">Cancels this caller's read or wait.</param>
    /// <returns>A read-only value, confirmed absence, or successful miss. Provider, validation, and resource failures remain failures.</returns>
    /// <remarks>A miss has both <see cref="CacheLookup{T}.Found" /> and <see cref="CacheLookup{T}.IsNotFound" /> set to false.</remarks>
    /// <example><code>var result = await collection.GetAsync("product:42", cancellationToken);</code></example>
    ValueTask<Result<CacheLookup<T>>> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Reads a valid entry or loads its source under the configured ownership and admission limits.</summary>
    /// <param name="item">The exact key and known dependencies captured for source work.</param>
    /// <param name="loader">A repeatable source read using the cache-owned context. A joining caller's delegate may never run.</param>
    /// <param name="cancellationToken">Cancels this caller's wait independently of other callers sharing the source work.</param>
    /// <returns>The value or confirmed absence with separate lookup and storage outcomes, or an explicit failure.</returns>
    /// <remarks>
    /// The configured strategy may coalesce source work or run separately admitted loads. The owner supplies the captured item and metadata.
    /// Loaders may be retried and must not perform business mutations. A successful source value can be returned even when publication fails.
    /// </remarks>
    /// <example><code>var result = await collection.GetOrLoadAsync(new("product:42"), loader, cancellationToken);</code></example>
    ValueTask<Result<CacheLookup<T>>> GetOrLoadAsync(CacheItem item, CacheLoadDelegate<T> loader, CancellationToken cancellationToken = default);

    /// <summary>Reads or loads with an explicit typed access mode and background classification.</summary>
    /// <param name="item">The exact key and known dependencies captured for source work.</param>
    /// <param name="loader">A repeatable source read using cache-owned scoped services.</param>
    /// <param name="options">The access choices within the collection's fixed limits. Null uses normal foreground access.</param>
    /// <param name="cancellationToken">Cancels this caller's wait independently of other interested callers.</param>
    /// <returns>A read-only source or cache outcome, or a validation, source, provider, or admission failure.</returns>
    /// <remarks>
    /// Bypass skips lookup and suppresses publication of bypass-owned work. A disabled typed collection also loads without retaining values.
    /// Background access avoids foreground activity updates. Nested refresh or bypass from an executing loader is rejected.
    /// </remarks>
    /// <example><code>var result = await collection.GetOrLoadAsync(item, loader, new(CacheAccessMode.Bypass), cancellationToken);</code></example>
    ValueTask<Result<CacheLookup<T>>> GetOrLoadAsync(CacheItem item, CacheLoadDelegate<T> loader, CacheAccessOptions options, CancellationToken cancellationToken = default);

    /// <summary>Reads or loads an ordered batch using one repeatable single-item loader.</summary>
    /// <param name="items">The bounded batch, without null members or equivalent duplicate keys. The engine captures it before starting work.</param>
    /// <param name="loader">The repeatable loader for each owned miss, using that item's cache-owned context.</param>
    /// <param name="cancellationToken">Cancels this caller's batch wait.</param>
    /// <returns>An outer admission result containing one independent result per input item in the original order.</returns>
    /// <remarks>Inspect every inner result. An outer success does not mean all items succeeded. Overlapping batches do not hold unfinished source callbacks while joining foreign work.</remarks>
    /// <example><code>var result = await collection.GetOrLoadManyAsync([new("product:42"), new("product:43")], loader, cancellationToken);</code></example>
    ValueTask<Result<IReadOnlyList<Result<CacheLookup<T>>>>> GetOrLoadManyAsync(
        IReadOnlyList<CacheItem> items, CacheLoadDelegate<T> loader, CancellationToken cancellationToken = default);

    /// <summary>Loads captured owned misses through a repeatable batch source and preserves input order in the results.</summary>
    /// <param name="items">The bounded batch, without null members or equivalent duplicate keys.</param>
    /// <param name="loader">The batch source, receiving only owned misses and yielding one independent completion per owned key.</param>
    /// <param name="cancellationToken">Cancels this caller's batch wait independently of other interested callers.</param>
    /// <returns>An outer admission result containing ordered per-item cache or source results, including partial failures.</returns>
    /// <remarks>
    /// Hits and foreign in-flight loads are excluded from the source context. Completions can arrive in any order and publish independently.
    /// Omitted keys receive incomplete failures. Unknown or duplicate completion keys are contract failures.
    /// </remarks>
    /// <example><code>var result = await collection.GetOrLoadManyAsync(items, batchLoader, cancellationToken);</code></example>
    ValueTask<Result<IReadOnlyList<Result<CacheLookup<T>>>>> GetOrLoadManyAsync(
        IReadOnlyList<CacheItem> items, CacheBatchLoadDelegate<T> loader, CancellationToken cancellationToken = default);

    /// <summary>Requests a fresh source load while preserving an independently valid old version.</summary>
    /// <param name="item">The exact key and captured dependencies for the source load.</param>
    /// <param name="loader">The repeatable source read using cache-owned scoped services.</param>
    /// <param name="cancellationToken">Cancels this caller's wait independently of other interested callers.</param>
    /// <returns>The source value or confirmed absence with its actual publication outcome, or an explicit failure.</returns>
    /// <remarks>Publication remains conditional on the observed version and invalidation fences. Failure does not remove a valid old version, and conflicts never retry unconditionally.</remarks>
    /// <example><code>var result = await collection.RefreshAsync(item, loader, cancellationToken);</code></example>
    ValueTask<Result<CacheLookup<T>>> RefreshAsync(CacheItem item, CacheLoadDelegate<T> loader, CancellationToken cancellationToken = default);

    /// <summary>Refreshes with an optional expected version and background classification.</summary>
    /// <param name="item">The stable source key and captured dimensions.</param>
    /// <param name="loader">The repeatable source loader executed in an owned scope.</param>
    /// <param name="options">The access mode, background classification, and optional acquired expected version. Null uses normal foreground access.</param>
    /// <param name="cancellationToken">This caller's wait cancellation token.</param>
    /// <returns>The actual source and publication result. A normal refresh whose selected version is no longer valid fails before source execution.</returns>
    /// <remarks>Background work avoids foreground activity and rechecks selected-version eligibility before source execution. The old valid entry remains available while refresh runs.</remarks>
    /// <example><code>await collection.RefreshAsync(item, loader, new(Background: true, ExpectedVersion: version), cancellationToken);</code></example>
    ValueTask<Result<CacheLookup<T>>> RefreshAsync(CacheItem item, CacheLoadDelegate<T> loader, CacheAccessOptions options, CancellationToken cancellationToken = default);

    /// <summary>Stores a derived value without an expected-version condition.</summary>
    /// <param name="item">The exact key and known dependencies for the new entry.</param>
    /// <param name="value">The value to encode, including a null or empty value permitted by the declared contract.</param>
    /// <param name="descriptor">Additional dependencies and bounded metadata overrides. Null uses collection defaults.</param>
    /// <param name="cancellationToken">Cancels this write or this caller's bounded wait.</param>
    /// <returns>The actual storage acknowledgement and a version only when publication is confirmed, or an explicit failure.</returns>
    /// <remarks>Encoding, admission, and invalidation fences still apply. A direct write does not establish when caller-managed source access occurred.</remarks>
    /// <example><code>var stored = await collection.SetAsync(new("product:42"), card, cancellationToken: cancellationToken);</code></example>
    ValueTask<Result<CacheWriteOutcome>> SetAsync(CacheItem item, T value, CacheEntryDescriptor descriptor = null, CancellationToken cancellationToken = default);

    /// <summary>Replaces an acquired version only if it is still current at publication.</summary>
    /// <param name="item">The exact key and known dependencies for the replacement entry.</param>
    /// <param name="value">The replacement read-only value, including a permitted null or empty value.</param>
    /// <param name="expectedVersion">The token acquired from a confirmed cache lookup or write.</param>
    /// <param name="descriptor">Additional dependencies and bounded metadata overrides. Null uses collection defaults.</param>
    /// <param name="cancellationToken">Cancels this write or this caller's bounded wait.</param>
    /// <returns>The actual storage acknowledgement, or a conflict when the expected version no longer matches.</returns>
    /// <remarks>The provider checks the version atomically at commit. A conflict preserves the current entry and never falls back to an unconditional write.</remarks>
    /// <example><code>var stored = await collection.SetAsync(item, replacement, lookup.Version.Value, cancellationToken: cancellationToken);</code></example>
    ValueTask<Result<CacheWriteOutcome>> SetAsync(CacheItem item, T value, CacheEntryVersion expectedVersion, CacheEntryDescriptor descriptor = null, CancellationToken cancellationToken = default);

    /// <summary>Stores a value only when the supplied condition holds at publication.</summary>
    /// <param name="item">The exact key and known dependencies for the entry.</param>
    /// <param name="value">The read-only value to encode, including a permitted null or empty value.</param>
    /// <param name="condition">The acquired expected version, create-only condition, or default for an unconditional write.</param>
    /// <param name="descriptor">Additional dependencies and bounded metadata overrides. Null uses collection defaults.</param>
    /// <param name="cancellationToken">Cancels this write or this caller's bounded wait.</param>
    /// <returns>The actual storage acknowledgement, or a conflict if the atomic condition fails.</returns>
    /// <remarks>Create-only can replace confirmed absence, but a positive null or empty value is present. Failed conditions do not overwrite the current entry or retry unconditionally.</remarks>
    /// <example><code>var created = await collection.SetAsync(item, card, new CacheWriteCondition(CreateOnly: true), cancellationToken: cancellationToken);</code></example>
    ValueTask<Result<CacheWriteOutcome>> SetAsync(CacheItem item, T value, CacheWriteCondition condition, CacheEntryDescriptor descriptor = null, CancellationToken cancellationToken = default);

    /// <summary>Validates and stores encoded input without materializing a typed value.</summary>
    /// <param name="item">The exact key and known dependencies for the entry.</param>
    /// <param name="input">The caller-owned encoded input, read from its current position. The cache neither retains nor disposes it.</param>
    /// <param name="descriptor">Additional dependencies and bounded metadata overrides. Null uses collection defaults.</param>
    /// <param name="condition">The atomic publication condition, or default for an unconditional write.</param>
    /// <param name="cancellationToken">Cancels the transfer or this caller's bounded wait.</param>
    /// <returns>The actual storage acknowledgement, or a failure for unsupported validation, invalid bytes, failed conditions, or resource limits.</returns>
    /// <remarks>The selected codec must have an explicitly registered, bounded, nonmaterializing streaming validator. Format compatibility and all normal publication checks still apply.</remarks>
    /// <example><code>var stored = await collection.SetEncodedAsync(item, encoded, cancellationToken: cancellationToken);</code></example>
    ValueTask<Result<CacheWriteOutcome>> SetEncodedAsync(CacheItem item, Stream input, CacheEntryDescriptor descriptor = null,
        CacheWriteCondition condition = default, CancellationToken cancellationToken = default);

    /// <summary>Acquires a snapshot of the collection's logical encoded bytes without decoding a typed value.</summary>
    /// <param name="key">The exact key using invariant case equivalence.</param>
    /// <param name="options">An optional expected version, seek requirement, or logical-byte range. Null requests a full forward read.</param>
    /// <param name="cancellationToken">Cancels this acquisition or this caller's bounded wait.</param>
    /// <returns>A caller-owned read handle, confirmed absence, a successful miss, or an explicit failure.</returns>
    /// <remarks>
    /// Dispose each acquired handle asynchronously. Handles have independent positions and bounded lifetimes.
    /// Seeking and ranges require explicit codec and provider support. Reading bytes does not prove integrity until terminal completion succeeds.
    /// </remarks>
    /// <example><code>
    /// var result = await collection.OpenReadAsync("product:42", cancellationToken: cancellationToken);
    /// if (result.IsSuccess &amp;&amp; result.Value.Found)
    /// {
    ///     await using var handle = result.Value.Value;
    ///     await handle.Stream.CopyToAsync(destination, cancellationToken);
    ///     var completion = await handle.WaitForCompletionAsync(cancellationToken);
    /// }
    /// </code></example>
    ValueTask<Result<CacheLookup<ICacheReadHandle>>> OpenReadAsync(string key, CacheReadOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>Removes an exact key and prevents previously started loads from publishing it.</summary>
    /// <param name="key">The exact key to remove, with no wildcard interpretation.</param>
    /// <param name="cancellationToken">Cancels the provider mutation or wait.</param>
    /// <returns>Success when the removal boundary is established, including an already missing key, or an explicit failure.</returns>
    /// <remarks>A missing key still advances the publication fence. Already acquired immutable read handles keep their snapshots.</remarks>
    /// <example><code>var removed = await collection.RemoveAsync("product:42", cancellationToken);</code></example>
    ValueTask<Result> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Removes a key only while its acquired expected version is current.</summary>
    /// <param name="key">The exact key to remove.</param>
    /// <param name="expectedVersion">The token acquired from a confirmed cache lookup or write.</param>
    /// <param name="cancellationToken">Cancels the provider mutation or wait.</param>
    /// <returns>Success when the matching version is removed, or a conflict if the condition fails.</returns>
    /// <remarks>A conflict preserves the current entry and does not advance invalidation fences.</remarks>
    /// <example><code>var removed = await collection.RemoveAsync("product:42", lookup.Version.Value, cancellationToken);</code></example>
    ValueTask<Result> RemoveAsync(string key, CacheEntryVersion expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Renews an eligible entry's sliding timeout without loading its source.</summary>
    /// <param name="key">The exact key whose authoritative validity is checked.</param>
    /// <param name="cancellationToken">Cancels the provider operation or wait.</param>
    /// <returns>The provider's renewal result, or an explicit failure.</returns>
    /// <remarks>Renewal cannot revive expired or invalidated content, extend the original absolute expiry, or reset source age.</remarks>
    /// <example><code>var touched = await collection.TouchAsync("product:42", cancellationToken);</code></example>
    ValueTask<Result> TouchAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Previews matching metadata within the collection's query limits.</summary>
    /// <param name="selection">The validated key or declared-dimension selection. An empty selection matches nothing.</param>
    /// <param name="cancellationToken">Cancels metadata enumeration or the wait.</param>
    /// <returns>The count, count accuracy, bounded sample, and any continuation, or an explicit failure.</returns>
    /// <remarks>Preview does not open payloads or change entries and fences. Its result is an observation, not a reservation for a later invalidation.</remarks>
    /// <example><code>var preview = await collection.PreviewInvalidationAsync(CacheSelection.Pattern("product:*"), cancellationToken);</code></example>
    ValueTask<Result<CacheInvalidationPreview>> PreviewInvalidationAsync(CacheSelection selection, CancellationToken cancellationToken = default);

    /// <summary>Invalidates matching entries and fences matching active loads through bounded provider work.</summary>
    /// <param name="selection">The validated key or declared-dimension selection. An empty selection matches nothing.</param>
    /// <param name="cancellationToken">Cancels provider invalidation or the wait.</param>
    /// <returns>The actual invalidation progress and continuation, or an explicit failure.</returns>
    /// <remarks>Check <see cref="CacheInvalidationOutcome.Completed" />. A successful partial outcome does not establish that every match was invalidated. Already acquired handles remain independent snapshots.</remarks>
    /// <example><code>var invalidated = await collection.InvalidateAsync(CacheSelection.Pattern("product:*"), cancellationToken);</code></example>
    ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheSelection selection, CancellationToken cancellationToken = default);

    /// <summary>Explicitly invalidates the whole logical collection across retained contract versions.</summary>
    /// <param name="cancellationToken">Cancels provider invalidation or the wait.</param>
    /// <returns>The actual invalidation progress and continuation, or an explicit failure.</returns>
    /// <remarks>Clear fences prior active loads and uses the same bounded work rules as selection invalidation. Inspect completion before assuming the entire collection is invalidated.</remarks>
    /// <example><code>var cleared = await collection.ClearAsync(cancellationToken);</code></example>
    ValueTask<Result<CacheInvalidationOutcome>> ClearAsync(CancellationToken cancellationToken = default);
}