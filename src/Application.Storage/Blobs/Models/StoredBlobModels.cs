// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes a provider-facing stream-first blob upload. The caller owns the input stream.
/// </summary>
/// <example>
/// <code>
/// var upload = new StoredBlobUpload
/// {
///     Key = new BlobKey("reports", "2026/06/report.pdf"),
///     Content = sourceStream,
///     ContentType = ContentType.PDF
/// };
/// </code>
/// </example>
public sealed class StoredBlobUpload
{
    /// <summary>Gets independently supplied or persisted Dimensions.</summary>
    public BlobDimensions Dimensions { get; init; }

    /// <summary>
    /// Gets the destination blob key.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = upload.Key;
    /// </code>
    /// </example>
    public BlobKey Key { get; init; }

    /// <summary>
    /// Gets the readable upload stream owned by the caller.
    /// </summary>
    /// <example>
    /// <code>
    /// var canRead = upload.Content.CanRead;
    /// </code>
    /// </example>
    public Stream Content { get; init; }

    /// <summary>
    /// Gets the provider-neutral content type when supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var mimeType = upload.ContentType?.MimeType();
    /// </code>
    /// </example>
    public ContentType? ContentType { get; init; }

    /// <summary>
    /// Gets the expected SHA-256 content hash when supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var expectedHash = upload.ExpectedContentHash;
    /// </code>
    /// </example>
    public string ExpectedContentHash { get; init; }

    /// <summary>
    /// Gets the optional UTC expiration timestamp after which retention sweeping may delete the blob.
    /// Providers normalize non-UTC offsets to UTC before storing metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// var expiresAt = upload.ExpiresAt;
    /// </code>
    /// </example>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets replacement scalar application properties. Null preserves existing properties; an empty bag clears them.
    /// </summary>
    /// <example>
    /// <code>
    /// // Clear application properties explicitly; omit Properties to preserve them.
    /// var clear = new PropertyBag();
    /// </code>
    /// </example>
    public PropertyBag Properties { get; init; }

    /// <summary>Gets the concrete current item ETag required for replacement; null is unconditional.</summary>
    /// <example><code>var etag = upload.IfMatchETag;</code></example>
    public string IfMatchETag { get; init; }

    /// <summary>Gets the overwrite behavior for the upload.</summary>
    /// <example><code>var mode = upload.OverwriteMode;</code></example>
    public BlobOverwriteMode OverwriteMode { get; init; } = BlobOverwriteMode.Overwrite;

    /// <summary>Gets provider-owned transform metadata, separate from application properties.</summary>
    /// <example><code>var metadata = model.TransformMetadata;</code></example>
    public PropertyBag TransformMetadata { get; init; } = new();
}

/// <summary>
/// Describes a stored blob with detached application properties and separate transform state.
/// </summary>
/// <example>
/// <code>
/// var info = new StoredBlobInfo
/// {
///     Key = new BlobKey("reports", "2026/06/report.pdf"),
///     Length = 1024,
///     ContentType = ContentType.PDF
/// };
/// </code>
/// </example>
public sealed class StoredBlobInfo
{
    /// <summary>Gets independently supplied or persisted Dimensions.</summary>
    public BlobDimensions Dimensions { get; init; } = BlobDimensions.Empty;

    /// <summary>
    /// Gets the key that identifies the blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = info.Key;
    /// </code>
    /// </example>
    public BlobKey Key { get; init; }

    /// <summary>
    /// Gets the blob content length in bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var length = info.Length;
    /// </code>
    /// </example>
    public long Length { get; init; }

    /// <summary>
    /// Gets the provider-neutral content type when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var mimeType = info.ContentType?.MimeType();
    /// </code>
    /// </example>
    public ContentType? ContentType { get; init; }

    /// <summary>
    /// Gets the provider-neutral SHA-256 content hash when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var hash = info.ContentHash;
    /// </code>
    /// </example>
    public string ContentHash { get; init; }

    /// <summary>
    /// Gets the provider-dependent entity tag when available.
    /// </summary>
    /// <example>
    /// <code>
    /// var etag = info.ETag;
    /// </code>
    /// </example>
    public string ETag { get; init; }

    /// <summary>
    /// Gets the provider creation timestamp when available.
    /// </summary>
    /// <example>
    /// <code>
    /// var created = info.CreatedAt;
    /// </code>
    /// </example>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Gets the provider last-modified timestamp when available.
    /// </summary>
    /// <example>
    /// <code>
    /// var modified = info.LastModifiedAt;
    /// </code>
    /// </example>
    public DateTimeOffset? LastModifiedAt { get; init; }

    /// <summary>
    /// Gets the optional UTC expiration timestamp after which retention sweeping may delete the blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var expiresAt = info.ExpiresAt;
    /// </code>
    /// </example>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets custom application properties associated with the blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var customerId = info.Properties.Get&lt;string&gt;("customerId");
    /// </code>
    /// </example>
    public PropertyBag Properties { get; init; } = new();

    /// <summary>Gets provider-owned transform metadata, separate from application properties.</summary>
    /// <example><code>var metadata = model.TransformMetadata;</code></example>
    public PropertyBag TransformMetadata { get; init; } = new();
}

/// <summary>
/// Represents a downloaded blob stream and its provider-neutral metadata.
/// </summary>
/// <example>
/// <code>
/// await using var download = result.Value;
/// await download.Content.CopyToAsync(targetStream, cancellationToken);
/// </code>
/// </example>
public sealed class StoredBlobDownload : IAsyncDisposable
{
    /// <summary>
    /// Gets the readable content stream owned by this download result.
    /// </summary>
    /// <example>
    /// <code>
    /// await download.Content.CopyToAsync(target, cancellationToken);
    /// </code>
    /// </example>
    public Stream Content { get; init; }

    /// <summary>
    /// Gets the blob information returned with the content stream.
    /// </summary>
    /// <example>
    /// <code>
    /// var length = download.Info.Length;
    /// </code>
    /// </example>
    public StoredBlobInfo Info { get; init; }

    /// <summary>
    /// Disposes the owned content stream.
    /// </summary>
    /// <returns>A task-like value representing asynchronous disposal.</returns>
    /// <example>
    /// <code>
    /// await download.DisposeAsync();
    /// </code>
    /// </example>
    public ValueTask DisposeAsync()
    {
        return this.Content.DisposeAsync();
    }
}

/// <summary>
/// Describes a blob property update that does not upload content.
/// </summary>
/// <example>
/// <code>
/// var update = new StoredBlobPropertiesUpdate
/// {
///     Key = new BlobKey("reports", "2026/06/report.pdf"),
///     ContentType = ContentType.PDF
/// };
/// </code>
/// </example>
public sealed class StoredBlobPropertiesUpdate
{
    /// <summary>
    /// Gets the optional replacement dimensions for this metadata write. Null preserves the existing collection.
    /// </summary>
    /// <example><code>var update = new StoredBlobPropertiesUpdate { Dimensions = BlobDimensions.Empty };</code></example>
    public BlobDimensions Dimensions { get; init; }

    /// <summary>
    /// Gets the blob key to update.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = update.Key;
    /// </code>
    /// </example>
    public BlobKey Key { get; init; }

    /// <summary>
    /// Gets the replacement content type when supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var mimeType = update.ContentType?.MimeType();
    /// </code>
    /// </example>
    public ContentType? ContentType { get; init; }

    /// <summary>
    /// Gets the replacement UTC expiration timestamp. A null value clears any existing expiration.
    /// Providers normalize non-UTC offsets to UTC before storing metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// update.ExpiresAt = DateTimeOffset.UtcNow.AddDays(30);
    /// </code>
    /// </example>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets the replacement custom property bag.
    /// </summary>
    /// <example>
    /// <code>
    /// var reviewed = update.Properties.Get&lt;bool&gt;("reviewed");
    /// </code>
    /// </example>
    public PropertyBag Properties { get; init; }

    /// <summary>
    /// Gets the optional entity tag required for optimistic property updates.
    /// </summary>
    /// <example>
    /// <code>
    /// var etag = update.IfMatchETag;
    /// </code>
    /// </example>
    public string IfMatchETag { get; init; }

    /// <summary>Gets replacement transform metadata. Null preserves the current transform state.</summary>
    /// <example><code>var metadata = model.TransformMetadata;</code></example>
    public PropertyBag TransformMetadata { get; init; }
}

/// <summary>
/// Represents one page of blob information returned from listing.
/// </summary>
/// <example>
/// <code>
/// foreach (var item in page.Items)
/// {
///     var name = item.Key.Name;
/// }
/// </code>
/// </example>
public sealed class StoredBlobPage
{
    /// <summary>
    /// Gets the provider-neutral blob information for this page.
    /// </summary>
    /// <example>
    /// <code>
    /// var count = page.Items.Count;
    /// </code>
    /// </example>
    public IReadOnlyCollection<StoredBlobInfo> Items { get; init; } = [];

    /// <summary>
    /// Gets the opaque continuation token when more results exist.
    /// </summary>
    /// <example>
    /// <code>
    /// var next = page.ContinuationToken;
    /// </code>
    /// </example>
    public string ContinuationToken { get; init; }

    /// <summary>
    /// Gets a value indicating whether the page has a continuation token.
    /// </summary>
    /// <example>
    /// <code>
    /// if (page.HasMore)
    /// {
    ///     // request the next page
    /// }
    /// </code>
    /// </example>
    public bool HasMore => !string.IsNullOrWhiteSpace(this.ContinuationToken);
}
