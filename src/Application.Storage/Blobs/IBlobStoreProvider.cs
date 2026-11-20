// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines the Result-native provider contract implemented by blob-store providers.
/// </summary>
/// <example>
/// <code>
/// var result = await provider.ExistsAsync(
///     new BlobKey("reports", "2026/06/report.pdf"),
///     cancellationToken);
/// </code>
/// </example>
public interface IBlobStoreProvider
{
    /// <summary>
    /// Gets the provider capabilities used by validation and diagnostics.
    /// </summary>
    /// <example>
    /// <code>
    /// var supportsListing = provider.Capabilities.SupportsPrefixListing;
    /// </code>
    /// </example>
    BlobStoreProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Uploads one blob from a caller-owned stream, conditionally replacing the whole item when IfMatchETag is supplied.
    /// </summary>
    /// <param name="upload">The upload model.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing the stored blob information.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.UploadAsync(upload, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<StoredBlobInfo>> UploadAsync(StoredBlobUpload upload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads one blob by exact key.
    /// </summary>
    /// <param name="key">The exact blob key.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing the download stream and metadata.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.DownloadAsync(key, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<StoredBlobDownload>> DownloadAsync(BlobKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets blob properties without downloading content.
    /// </summary>
    /// <param name="key">The exact blob key.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing blob information.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.GetPropertiesAsync(key, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<StoredBlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates blob properties without downloading content.
    /// </summary>
    /// <param name="update">The property update model.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing updated blob information.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.UpdatePropertiesAsync(update, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<StoredBlobInfo>> UpdatePropertiesAsync(StoredBlobPropertiesUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks exact-key existence.
    /// </summary>
    /// <param name="key">The exact blob key.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing <c>true</c> when the blob exists; otherwise <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.ExistsAsync(key, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists one bounded page of blob information.
    /// </summary>
    /// <param name="query">The normalized listing query.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing one page of blob information.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.ListPageAsync(query, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<StoredBlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a blob by exact key.
    /// </summary>
    /// <param name="key">The exact blob key.</param>
    /// <param name="options">Optional conditional delete settings.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result indicating whether the delete completed successfully.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.DeleteAsync(key, new BlobDeleteOptions { IfMatchETag = etag }, cancellationToken);
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
    /// <example><code>var result = await provider.UpdateDimensionsAsync(new() { Key = key, Dimensions = BlobDimensions.Empty, IfMatchETag = current.ETag }, cancellationToken);</code></example>
    Task<Result<StoredBlobInfo>> UpdateDimensionsAsync(BlobDimensionsUpdate update, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<StoredBlobInfo>.Failure(new BlobDimensionsNotSupportedError()));

}
