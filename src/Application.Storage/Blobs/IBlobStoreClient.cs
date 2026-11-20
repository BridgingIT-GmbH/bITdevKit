// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines the public Result-native API used by application code to store and query blobs.
/// </summary>
/// <example>
/// <code>
/// var result = await blobs.ExistsAsync(
///     new BlobKey("reports", "2026/06/report.pdf"),
///     cancellationToken);
/// </code>
/// </example>
public interface IBlobStoreClient
{
    /// <summary>Gets the current provider's indexed Dimensions profile.</summary>
    /// <example><code>var supported = blobs.DimensionCapabilities.SupportsDimensions;</code></example>
    BlobDimensionCapabilities DimensionCapabilities => BlobDimensionCapabilities.Unsupported;

    /// <summary>
    /// Uploads one blob from a caller-owned stream, conditionally replacing the whole item when IfMatchETag is supplied.
    /// </summary>
    /// <param name="upload">The upload model.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing the stored blob information.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.UploadAsync(upload, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<BlobInfo>> UploadAsync(BlobUpload upload, CancellationToken cancellationToken = default);

    /// <summary>Attempts every independent upload with bounded concurrency and returns per-item outcomes in input order.</summary>
    /// <param name="uploads">At most 1000 requests with distinct keys and caller-owned stream instances.</param>
    /// <param name="options">Optional worker limit, defaulting to four active uploads.</param>
    /// <param name="cancellationToken">Cancels outstanding work; previously committed uploads remain committed.</param>
    /// <returns>A completed batch with individual successes/failures, or an outer failure for invalid batch options.</returns>
    /// <remarks>Streams remain owned by the caller. The batch does not promise atomic publication or execution order. Duplicate keys/shared streams fail individually. Cancellation throws after active work observes cancellation; no complete outcome collection is returned. The batch itself never retries.</remarks>
    /// <example><code>var batch = await blobs.UploadManyAsync(uploads, new() { MaxConcurrency = 4 });</code></example>
    Task<Result<BlobUploadBatchResult>> UploadManyAsync(IReadOnlyCollection<BlobUpload> uploads,
        BlobUploadBatchOptions options = null, CancellationToken cancellationToken = default) =>
        BlobUploadBatchExecutor.ExecuteAsync(this, uploads, options, cancellationToken);

    /// <summary>
    /// Downloads one blob by exact key.
    /// </summary>
    /// <param name="key">The exact blob key.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing the download stream and metadata.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.DownloadAsync(key, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<BlobDownload>> DownloadAsync(BlobKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets blob properties without downloading content.
    /// </summary>
    /// <param name="key">The exact blob key.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing blob information.</returns>
    /// <example>
    /// <code>
    /// var properties = await blobs.GetPropertiesAsync(key, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates blob properties without downloading content.
    /// </summary>
    /// <param name="update">The property update model.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing updated blob information.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.UpdatePropertiesAsync(update, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<BlobInfo>> UpdatePropertiesAsync(BlobPropertiesUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks exact-key existence.
    /// </summary>
    /// <param name="key">The exact blob key.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing <c>true</c> when the blob exists; otherwise <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// var exists = await blobs.ExistsAsync(key, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists one bounded page of blob information.
    /// </summary>
    /// <param name="query">The listing query.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing one page of blob information.</returns>
    /// <example>
    /// <code>
    /// var page = await blobs.ListPageAsync(query, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a blob by exact key.
    /// </summary>
    /// <param name="key">The exact blob key.</param>
    /// <param name="options">Optional conditional delete settings.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result indicating whether the delete completed successfully.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.DeleteAsync(key, new BlobDeleteOptions { IfMatchETag = etag }, cancellationToken);
    /// </code>
    /// </example>
    Task<Result> DeleteAsync(
        BlobKey key,
        BlobDeleteOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces Dimensions under the existing whole-item ETag without rewriting content.</summary>
    /// <param name="update">The exact key, required replacement collection, and optional current whole-item ETag.</param>
    /// <param name="cancellationToken">Cancels provider I/O.</param>
    /// <returns>The acknowledged metadata snapshot or a typed failure.</returns>
    /// <example><code>var result = await client.UpdateDimensionsAsync(new() { Key = key, Dimensions = BlobDimensions.Empty, IfMatchETag = current.ETag }, cancellationToken);</code></example>
    Task<Result<BlobInfo>> UpdateDimensionsAsync(BlobDimensionsUpdate update, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<BlobInfo>.Failure(new BlobDimensionsNotSupportedError()));

}