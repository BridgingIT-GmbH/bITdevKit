// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Globalization;

/// <summary>
/// Decorates an <see cref="IBlobStoreClient" /> with transparent GZip compression and decompression.
/// </summary>
/// <remarks>
/// Uploads are transformed into compressed bytes before reaching the inner client. Downloads are decompressed before
/// the caller receives the content stream. Internal behavior metadata is removed from public results.
/// </remarks>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithCompressionBehavior()
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
public sealed class CompressionBlobStoreClientBehavior : IBlobStoreClient
{
    private const string Algorithm = "gzip";

    private readonly IBlobStoreClient inner;
    private readonly CompressionBlobStoreClientBehaviorOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompressionBlobStoreClientBehavior" /> class.
    /// </summary>
    /// <param name="inner">The decorated blob-store client.</param>
    /// <param name="options">The compression behavior options.</param>
    /// <param name="storeName">The configured blob-store client name.</param>
    /// <example>
    /// <code>
    /// var client = new CompressionBlobStoreClientBehavior(inner, new CompressionBlobStoreClientBehaviorOptions(), "reports");
    /// </code>
    /// </example>
    public CompressionBlobStoreClientBehavior(
        IBlobStoreClient inner,
        CompressionBlobStoreClientBehaviorOptions options = null,
        string storeName = null)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.options = options ?? new CompressionBlobStoreClientBehaviorOptions();
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
    public async Task<Result<BlobInfo>> UploadAsync(
        BlobUpload upload,
        CancellationToken cancellationToken = default)
    {
        var contentValidation = BlobBehaviorTransform.ValidateContent(upload?.Content);
        if (contentValidation.IsFailure)
        {
            return Result<BlobInfo>.Failure(contentValidation);
        }

        var transformed = BlobBehaviorTransform.CreateTemporaryStream();
        try
        {
            BlobTransformCopyResult copy;
            await using (var gzip = CompressionHelper.CreateGZipCompressionStream(
                transformed,
                this.options.Level,
                leaveOpen: true))
            {
                copy = await BlobBehaviorTransform.CopyAndHashAsync(upload.Content, gzip, cancellationToken).ConfigureAwait(false);
            }

            var expectedHash = BlobBehaviorTransform.ValidateExpectedHash(upload.ExpectedContentHash, copy.ContentHash);
            if (expectedHash.IsFailure)
            {
                await transformed.DisposeAsync().ConfigureAwait(false);
                return Result<BlobInfo>.Failure(expectedHash);
            }

            transformed.Position = 0;
            var properties = BlobBehaviorTransform.CloneProperties(upload.Properties);
            properties[BlobBehaviorTransform.CompressionAlgorithmKey] = Algorithm;
            properties[BlobBehaviorTransform.CompressionLengthKey] = copy.Length.ToString(CultureInfo.InvariantCulture);
            properties[BlobBehaviorTransform.CompressionContentTypeKey] = upload.ContentType?.ToString() ?? string.Empty;
            properties[BlobBehaviorTransform.CompressionContentHashKey] = copy.ContentHash;

            var result = await this.inner.UploadAsync(new BlobUpload
            {
                Key = upload.Key,
                Content = transformed,
                ContentType = this.options.StoredContentType,
                ExpectedContentHash = null,
                ExpiresAt = upload.ExpiresAt,
                Properties = properties,
                OverwriteMode = upload.OverwriteMode
            }, cancellationToken).ConfigureAwait(false);

            await transformed.DisposeAsync().ConfigureAwait(false);

            return result.IsSuccess
                ? Result<BlobInfo>.Success(this.ToLogicalInfo(result.Value))
                : result;
        }
        catch (InvalidDataException exception)
        {
            await transformed.DisposeAsync().ConfigureAwait(false);
            return Result<BlobInfo>.Failure(new BlobStoreIntegrityError($"Blob compression failed: {exception.Message}"));
        }
        catch
        {
            await transformed.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<BlobDownload>> DownloadAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        var result = await this.inner.DownloadAsync(key, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result;
        }

        if (!IsCompressed(result.Value.Info))
        {
            return Result<BlobDownload>.Success(new BlobDownload
            {
                Content = result.Value.Content,
                Info = this.ToLogicalInfo(result.Value.Info)
            });
        }

        var transformed = BlobBehaviorTransform.CreateTemporaryStream();
        try
        {
            await using (result.Value.ConfigureAwait(false))
            {
                await using var gzip = CompressionHelper.CreateGZipDecompressionStream(result.Value.Content, leaveOpen: false);
                var copy = await BlobBehaviorTransform.CopyAndHashAsync(gzip, transformed, cancellationToken).ConfigureAwait(false);
                var logicalInfo = this.ToLogicalInfo(result.Value.Info, copy);
                var integrity = BlobBehaviorTransform.ValidateExpectedHash(logicalInfo.ContentHash, copy.ContentHash);
                if (integrity.IsFailure)
                {
                    await transformed.DisposeAsync().ConfigureAwait(false);
                    return Result<BlobDownload>.Failure(integrity);
                }

                transformed.Position = 0;
                return Result<BlobDownload>.Success(new BlobDownload
                {
                    Content = transformed,
                    Info = logicalInfo
                });
            }
        }
        catch (InvalidDataException exception)
        {
            await transformed.DisposeAsync().ConfigureAwait(false);
            return Result<BlobDownload>.Failure(new BlobStoreIntegrityError($"Blob decompression failed: {exception.Message}"));
        }
        catch
        {
            await transformed.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> GetPropertiesAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        var result = await this.inner.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Result<BlobInfo>.Success(this.ToLogicalInfo(result.Value))
            : result;
    }

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> UpdatePropertiesAsync(
        BlobPropertiesUpdate update,
        CancellationToken cancellationToken = default)
    {
        var current = await this.inner.GetPropertiesAsync(update.Key, cancellationToken).ConfigureAwait(false);
        if (current.IsFailure)
        {
            return current;
        }

        var innerUpdate = BlobBehaviorTransform.PreserveInternalProperties(
            update,
            current.Value,
            BlobBehaviorTransform.CompressionAlgorithmKey,
            BlobBehaviorTransform.CompressionLengthKey,
            BlobBehaviorTransform.CompressionContentTypeKey,
            BlobBehaviorTransform.CompressionContentHashKey);

        var result = await this.inner.UpdatePropertiesAsync(innerUpdate, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Result<BlobInfo>.Success(this.ToLogicalInfo(result.Value))
            : result;
    }

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default) =>
        this.inner.ExistsAsync(key, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<BlobPage>> ListPageAsync(
        BlobQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await this.inner.ListPageAsync(query, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Result<BlobPage>.Success(BlobBehaviorTransform.ApplyLogicalPage(result.Value, info => this.ToLogicalInfo(info)))
            : result;
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(
        BlobKey key,
        BlobDeleteOptions options = null,
        CancellationToken cancellationToken = default) =>
        this.inner.DeleteAsync(key, options, cancellationToken);

    private static bool IsCompressed(BlobInfo info) =>
        string.Equals(
            info?.Properties?.Get<string>(BlobBehaviorTransform.CompressionAlgorithmKey),
            Algorithm,
            StringComparison.OrdinalIgnoreCase);

    private BlobInfo ToLogicalInfo(BlobInfo info, BlobTransformCopyResult copy = null)
    {
        var mapped = BlobBehaviorTransform.ApplyLogicalInfo(
            info,
            BlobBehaviorTransform.CompressionAlgorithmKey,
            BlobBehaviorTransform.CompressionLengthKey,
            BlobBehaviorTransform.CompressionContentTypeKey,
            BlobBehaviorTransform.CompressionContentHashKey);

        return copy is null || mapped is null
            ? mapped
            : new BlobInfo
            {
                Key = mapped.Key,
                Length = copy.Length,
                ContentType = mapped.ContentType,
                ContentHash = copy.ContentHash,
                ETag = mapped.ETag,
                CreatedAt = mapped.CreatedAt,
                LastModifiedAt = mapped.LastModifiedAt,
                ExpiresAt = mapped.ExpiresAt,
                Properties = mapped.Properties
            };
    }
}
