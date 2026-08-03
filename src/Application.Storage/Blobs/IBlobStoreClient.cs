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
    /// <summary>
    /// Uploads one blob from a caller-owned stream.
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
}
