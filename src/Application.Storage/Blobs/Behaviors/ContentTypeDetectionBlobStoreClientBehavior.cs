// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Detects missing upload content types from blob name extensions without reading blob content.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithContentTypeDetectionBehavior()
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
public sealed class ContentTypeDetectionBlobStoreClientBehavior : IBlobStoreClient
{
    private readonly IBlobStoreClient inner;
    private readonly ContentTypeDetectionBlobStoreClientBehaviorOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentTypeDetectionBlobStoreClientBehavior" /> class.
    /// </summary>
    /// <param name="inner">The decorated blob-store client.</param>
    /// <param name="options">The detection options.</param>
    /// <param name="storeName">The configured blob-store client name.</param>
    /// <example>
    /// <code>
    /// var behavior = new ContentTypeDetectionBlobStoreClientBehavior(inner, options, "reports");
    /// </code>
    /// </example>
    public ContentTypeDetectionBlobStoreClientBehavior(
        IBlobStoreClient inner,
        ContentTypeDetectionBlobStoreClientBehaviorOptions options = null,
        string storeName = null)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.options = options ?? new ContentTypeDetectionBlobStoreClientBehaviorOptions();
        this.StoreName = string.IsNullOrWhiteSpace(storeName) ? "default" : storeName;
    }

    /// <summary>
    /// Gets the configured blob-store client name.
    /// </summary>
    /// <example>
    /// <code>
    /// var store = behavior.StoreName;
    /// </code>
    /// </example>
    public string StoreName { get; }

    /// <inheritdoc />
    public Task<Result<BlobInfo>> UploadAsync(
        BlobUpload upload,
        CancellationToken cancellationToken = default)
    {
        if (upload?.ContentType is not null)
        {
            return this.inner.UploadAsync(upload, cancellationToken);
        }

        var contentType = this.Detect(upload?.Key?.Name);
        if (contentType is null)
        {
            return this.inner.UploadAsync(upload, cancellationToken);
        }

        return this.inner.UploadAsync(new BlobUpload
        {
            Key = upload.Key,
            Content = upload.Content,
            ContentType = contentType,
            ExpectedContentHash = upload.ExpectedContentHash,
            ExpiresAt = upload.ExpiresAt,
            Properties = upload.Properties,
            OverwriteMode = upload.OverwriteMode
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<BlobDownload>> DownloadAsync(BlobKey key, CancellationToken cancellationToken = default) =>
        this.inner.DownloadAsync(key, cancellationToken);

    /// <inheritdoc />
    public Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default) =>
        this.inner.GetPropertiesAsync(key, cancellationToken);

    /// <inheritdoc />
    public Task<Result<BlobInfo>> UpdatePropertiesAsync(BlobPropertiesUpdate update, CancellationToken cancellationToken = default) =>
        this.inner.UpdatePropertiesAsync(update, cancellationToken);

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default) =>
        this.inner.ExistsAsync(key, cancellationToken);

    /// <inheritdoc />
    public Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default) =>
        this.inner.ListPageAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<Result> DeleteAsync(
        BlobKey key,
        BlobDeleteOptions options = null,
        CancellationToken cancellationToken = default) =>
        this.inner.DeleteAsync(key, options, cancellationToken);

    private ContentType? Detect(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !HasFileExtension(name))
        {
            return null;
        }

        return ContentTypeExtensions.FromFileName(name, this.options.DefaultContentType);
    }

    private static bool HasFileExtension(string name)
    {
        var slashIndex = name.LastIndexOfAny(['/', '\\']);
        var dotIndex = name.LastIndexOf('.');

        return dotIndex > slashIndex && dotIndex < name.Length - 1;
    }
}
