// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Owns an independently positioned, lifetime-bounded snapshot stream and its provider reservations.</summary>
/// <remarks>
/// Dispose the handle asynchronously to release the snapshot and its resource reservations. Consume one handle sequentially, without concurrent reads or seeks.
/// An acquired snapshot remains independent of later replacement or invalidation, but its configured transfer deadlines still apply.
/// Payload integrity, destination writes, and transport delivery are separate outcomes. Successful acquisition alone does not establish payload integrity.
/// </remarks>
/// <example><code>await using var handle = result.Value.Value; await handle.Stream.CopyToAsync(destination, cancellationToken);</code></example>
public interface ICacheReadHandle : IAsyncDisposable
{
    /// <summary>Gets logical bytes, with cache-managed envelopes/compression removed by the engine.</summary>
    /// <value>The handle-owned stream. Its seekability and range are limited to the capabilities explicitly acquired.</value>
    /// <remarks>Read failures can throw <see cref="CacheTransferException" />. Dispose the handle even when consumption fails or stops early.</remarks>
    /// <example><code>await handle.Stream.CopyToAsync(destination, cancellationToken);</code></example>
    Stream Stream { get; }

    /// <summary>Gets metadata for the same acquired immutable version.</summary>
    /// <value>The identity, version, and bounded entry facts associated with this stream's snapshot.</value>
    /// <example><code>var version = handle.Metadata.Version;</code></example>
    CacheEntryMetadata Metadata { get; }

    /// <summary>Gets the explicitly requested logical range, or null for a full/explicitly seekable handle.</summary>
    /// <value>The acquired logical offset and byte count. Range completion does not assert whole-entry integrity.</value>
    /// <example><code>var offset = handle.Range?.Offset ?? 0;</code></example>
    CacheReadRange? Range { get; }

    /// <summary>Gets the integrity scope established by a successful terminal completion. Pending/incomplete/failed consumption establishes no such claim.</summary>
    /// <value>The complete entry or the affected stored frames, as determined by the acquired access mode.</value>
    /// <example><code>var scope = handle.IntegrityScope;</code></example>
    CacheReadIntegrityScope IntegrityScope { get; }

    /// <summary>Observes completion without reading, draining, rewinding, or owning the destination.</summary>
    /// <param name="cancellationToken">Cancels only this observation wait.</param>
    /// <returns>Validated full consumption, completed range consumption, incomplete consumption, or a classified terminal failure.</returns>
    /// <remarks>
    /// Consume or dispose the stream before waiting for a terminal outcome, or observe it concurrently with consumption.
    /// Cancelling this observation does not cancel consumption or dispose the handle. Successful validation makes no claim about a destination or transport.
    /// </remarks>
    /// <example><code>
    /// await handle.Stream.CopyToAsync(destination, cancellationToken);
    /// var completion = await handle.WaitForCompletionAsync(cancellationToken);
    /// if (completion.IsSuccess &amp;&amp; completion.Value == CacheReadCompletion.Validated) { ConfirmPayloadIntegrity(); }
    /// </code></example>
    ValueTask<Result<CacheReadCompletion>> WaitForCompletionAsync(CancellationToken cancellationToken = default);
}

/// <summary>Identifies one requested contiguous logical-byte range without copying the immutable entry metadata.</summary>
/// <param name="Offset">Zero-based logical offset.</param>
/// <param name="Length">Positive requested byte count.</param>
/// <example><code>var range = handle.Range;</code></example>
public readonly record struct CacheReadRange(long Offset, long Length);

/// <summary>Identifies the promised integrity boundary for a successful completed transfer.</summary>
/// <example><code>var isWholeEntry = handle.IntegrityScope == CacheReadIntegrityScope.FullEntry;</code></example>
public enum CacheReadIntegrityScope
{
    /// <summary>The complete logical payload and stored envelope are checked.</summary>
    /// <example><code>var scope = CacheReadIntegrityScope.FullEntry;</code></example>
    FullEntry,

    /// <summary>Every affected stored frame is checked; complete entry integrity is not asserted.</summary>
    /// <example><code>var scope = CacheReadIntegrityScope.AffectedStoredFrames;</code></example>
    AffectedStoredFrames
}