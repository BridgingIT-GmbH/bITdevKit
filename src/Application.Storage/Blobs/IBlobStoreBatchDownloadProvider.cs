// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines an optional provider capability that reads many small blobs with few round trips.
/// <c>DownloadBytesManyAsync</c> uses it automatically, while every key still passes through the
/// regular client behavior pipeline (compression, encryption, caching, retry and so on).
/// </summary>
/// <example>
/// <code>
/// public sealed class MyProvider : IBlobStoreProvider, IBlobStoreBatchDownloadProvider
/// {
///     public int MaxDownloadBatchSize => 25;
///
///     public Task&lt;IReadOnlyDictionary&lt;BlobKey, Result&lt;BlobDownload&gt;&gt;&gt; DownloadManyAsync(
///         IReadOnlyCollection&lt;BlobKey&gt; keys,
///         CancellationToken cancellationToken = default) =&gt; ...;
/// }
/// </code>
/// </example>
public interface IBlobStoreBatchDownloadProvider
{
    /// <summary>
    /// Gets the maximum number of keys passed to one <see cref="DownloadManyAsync" /> call.
    /// </summary>
    /// <example>
    /// <code>
    /// var batchSize = provider.MaxDownloadBatchSize;
    /// </code>
    /// </example>
    int MaxDownloadBatchSize { get; }

    /// <summary>
    /// Downloads many blobs by exact key with as few round trips as the provider can manage.
    /// </summary>
    /// <param name="keys">The distinct exact blob keys, at most <see cref="MaxDownloadBatchSize" />.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A dictionary with one result per key the provider could satisfy. Keys that are missing from the
    /// dictionary (for example blobs too large to buffer, or a failed batch) are read with
    /// <see cref="IBlobStoreProvider.DownloadAsync" /> instead, so a batch never changes the outcome of a read.
    /// The caller owns and disposes every returned <see cref="BlobDownload" />.
    /// </returns>
    /// <example>
    /// <code>
    /// var results = await provider.DownloadManyAsync(keys, cancellationToken);
    /// </code>
    /// </example>
    Task<IReadOnlyDictionary<BlobKey, Result<BlobDownload>>> DownloadManyAsync(
        IReadOnlyCollection<BlobKey> keys,
        CancellationToken cancellationToken = default);
}
