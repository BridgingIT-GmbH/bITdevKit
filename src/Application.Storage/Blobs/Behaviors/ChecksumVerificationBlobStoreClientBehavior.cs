// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Verifies downloaded blob content against <see cref="BlobInfo.ContentHash" /> before returning the stream to callers.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithChecksumVerificationBehavior()
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
public sealed class ChecksumVerificationBlobStoreClientBehavior : IBlobStoreClient
{
    private readonly IBlobStoreClient inner;
    private readonly ChecksumVerificationBlobStoreClientBehaviorOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChecksumVerificationBlobStoreClientBehavior" /> class.
    /// </summary>
    /// <param name="inner">The decorated blob-store client.</param>
    /// <param name="options">The checksum verification options.</param>
    /// <param name="storeName">The configured blob-store client name.</param>
    /// <example>
    /// <code>
    /// var behavior = new ChecksumVerificationBlobStoreClientBehavior(inner, options, "reports");
    /// </code>
    /// </example>
    public ChecksumVerificationBlobStoreClientBehavior(
        IBlobStoreClient inner,
        ChecksumVerificationBlobStoreClientBehaviorOptions options = null,
        string storeName = null)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.options = options ?? new ChecksumVerificationBlobStoreClientBehaviorOptions();
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
    public Task<Result<BlobInfo>> UploadAsync(BlobUpload upload, CancellationToken cancellationToken = default) =>
        this.inner.UploadAsync(upload, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<BlobDownload>> DownloadAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        var optionsValidation = this.options.Validate();
        if (optionsValidation.IsFailure)
        {
            return Result<BlobDownload>.Failure(optionsValidation);
        }

        var result = await this.inner.DownloadAsync(key, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result;
        }

        if (string.IsNullOrWhiteSpace(result.Value.Info?.ContentHash))
        {
            if (this.options.AllowMissingContentHash)
            {
                return result;
            }

            await result.Value.DisposeAsync().ConfigureAwait(false);
            return Result<BlobDownload>.Failure(new BlobStoreIntegrityError("Blob content hash is required for checksum verification."));
        }

        var temp = BlobBehaviorTransform.CreateTemporaryStream();
        try
        {
            await using (result.Value.ConfigureAwait(false))
            {
                var copy = await BlobVerifiedDownloadExtensions.CopyAndHashAsync(
                    result.Value.Content,
                    temp,
                    this.options.BufferSize,
                    cancellationToken).ConfigureAwait(false);
                if (copy.IsFailure)
                {
                    await temp.DisposeAsync().ConfigureAwait(false);
                    return Result<BlobDownload>.Failure(copy);
                }

                if (!string.Equals(result.Value.Info.ContentHash, copy.Value.Hash, StringComparison.Ordinal))
                {
                    await temp.DisposeAsync().ConfigureAwait(false);
                    return Result<BlobDownload>.Failure(new BlobStoreIntegrityError("Downloaded content hash does not match blob metadata."));
                }

                temp.Position = 0;
                return Result<BlobDownload>.Success(new BlobDownload
                {
                    Content = temp,
                    Info = result.Value.Info
                });
            }
        }
        catch
        {
            await temp.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

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
}
