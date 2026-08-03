// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Globalization;
using System.Security.Cryptography;

/// <summary>
/// Decorates an <see cref="IBlobStoreClient" /> with transparent AES encryption and decryption.
/// </summary>
/// <remarks>
/// Uploads are encrypted before reaching the inner client. Downloads are decrypted before the caller receives the
/// content stream. Internal behavior metadata is removed from public results.
/// </remarks>
/// <example>
/// <code>
/// services.AddSingleton&lt;IEncryptionKeyProvider&gt;(keyProvider);
/// services.AddBlobStorage()
///     .WithEncryptionBehavior()
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
public sealed class EncryptionBlobStoreClientBehavior : IBlobStoreClient
{
    private const string Algorithm = EncryptionHelper.AesCbcPkcs7Algorithm;

    private readonly IBlobStoreClient inner;
    private readonly EncryptionBlobStoreClientBehaviorOptions options;
    private readonly IEncryptionKeyProvider keyProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptionBlobStoreClientBehavior" /> class.
    /// </summary>
    /// <param name="inner">The decorated blob-store client.</param>
    /// <param name="keyProvider">The encryption-key provider.</param>
    /// <param name="options">The encryption behavior options.</param>
    /// <param name="storeName">The configured blob-store client name.</param>
    /// <example>
    /// <code>
    /// var client = new EncryptionBlobStoreClientBehavior(inner, keyProvider, options, "reports");
    /// </code>
    /// </example>
    public EncryptionBlobStoreClientBehavior(
        IBlobStoreClient inner,
        IEncryptionKeyProvider keyProvider,
        EncryptionBlobStoreClientBehaviorOptions options = null,
        string storeName = null)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        this.options = options ?? new EncryptionBlobStoreClientBehaviorOptions();
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
        var activeKey = await this.keyProvider.GetActiveKeyAsync(cancellationToken).ConfigureAwait(false);
        if (activeKey is null)
        {
            return Result<BlobInfo>.Failure(new BlobStoreValidationError("An active blob encryption key is required."));
        }

        var key = activeKey.Key.ToArray();
        if (!EncryptionHelper.IsValidAesKey(key))
        {
            return Result<BlobInfo>.Failure(new BlobStoreValidationError("Blob encryption key must be 16, 24, or 32 bytes."));
        }

        var contentValidation = BlobBehaviorTransform.ValidateContent(upload?.Content);
        if (contentValidation.IsFailure)
        {
            return Result<BlobInfo>.Failure(contentValidation);
        }

        var transformed = BlobBehaviorTransform.CreateTemporaryStream();
        try
        {
            var initializationVector = EncryptionHelper.GenerateAesCbcInitializationVector();
            BlobTransformCopyResult copy;
            await using (var crypto = EncryptionHelper.CreateAesCbcEncryptionStream(
                transformed,
                key,
                initializationVector,
                leaveOpen: true))
            {
                copy = await BlobBehaviorTransform.CopyAndHashAsync(upload.Content, crypto, cancellationToken).ConfigureAwait(false);
                crypto.FlushFinalBlock();
            }

            var expectedHash = BlobBehaviorTransform.ValidateExpectedHash(upload.ExpectedContentHash, copy.ContentHash);
            if (expectedHash.IsFailure)
            {
                await transformed.DisposeAsync().ConfigureAwait(false);
                return Result<BlobInfo>.Failure(expectedHash);
            }

            transformed.Position = 0;
            var properties = BlobBehaviorTransform.CloneProperties(upload.Properties);
            properties[BlobBehaviorTransform.EncryptionAlgorithmKey] = Algorithm;
            properties[BlobBehaviorTransform.EncryptionLengthKey] = copy.Length.ToString(CultureInfo.InvariantCulture);
            properties[BlobBehaviorTransform.EncryptionContentTypeKey] = upload.ContentType?.ToString() ?? string.Empty;
            properties[BlobBehaviorTransform.EncryptionContentHashKey] = copy.ContentHash;
            properties[BlobBehaviorTransform.EncryptionKeyIdKey] = activeKey.KeyId;
            properties[BlobBehaviorTransform.EncryptionIvKey] = Convert.ToBase64String(initializationVector);

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
        catch (CryptographicException exception)
        {
            await transformed.DisposeAsync().ConfigureAwait(false);
            return Result<BlobInfo>.Failure(new BlobStoreIntegrityError($"Blob encryption failed: {exception.Message}"));
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

        if (!IsEncrypted(result.Value.Info))
        {
            return Result<BlobDownload>.Success(new BlobDownload
            {
                Content = result.Value.Content,
                Info = this.ToLogicalInfo(result.Value.Info)
            });
        }

        var ivResult = GetIv(result.Value.Info);
        if (ivResult.IsFailure)
        {
            await result.Value.DisposeAsync().ConfigureAwait(false);
            return Result<BlobDownload>.Failure(ivResult);
        }

        var keyId = result.Value.Info?.Properties?.Get<string>(BlobBehaviorTransform.EncryptionKeyIdKey);
        if (string.IsNullOrWhiteSpace(keyId))
        {
            await result.Value.DisposeAsync().ConfigureAwait(false);
            return Result<BlobDownload>.Failure(new BlobStoreIntegrityError("Encrypted blob metadata does not contain a key identifier."));
        }

        var resolvedKey = await this.keyProvider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
        if (resolvedKey is null || !EncryptionHelper.IsValidAesKey(resolvedKey.Key.ToArray()))
        {
            await result.Value.DisposeAsync().ConfigureAwait(false);
            return Result<BlobDownload>.Failure(new BlobStoreIntegrityError($"Blob encryption key '{keyId}' is unavailable or invalid."));
        }

        var transformed = BlobBehaviorTransform.CreateTemporaryStream();
        try
        {
            await using (result.Value.ConfigureAwait(false))
            {
                await using var crypto = EncryptionHelper.CreateAesCbcDecryptionStream(
                    result.Value.Content,
                    resolvedKey.Key.ToArray(),
                    ivResult.Value,
                    leaveOpen: false);
                var copy = await BlobBehaviorTransform.CopyAndHashAsync(crypto, transformed, cancellationToken).ConfigureAwait(false);
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
        catch (CryptographicException exception)
        {
            await transformed.DisposeAsync().ConfigureAwait(false);
            return Result<BlobDownload>.Failure(new BlobStoreIntegrityError($"Blob decryption failed: {exception.Message}"));
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
            BlobBehaviorTransform.EncryptionAlgorithmKey,
            BlobBehaviorTransform.EncryptionLengthKey,
            BlobBehaviorTransform.EncryptionContentTypeKey,
            BlobBehaviorTransform.EncryptionContentHashKey,
            BlobBehaviorTransform.EncryptionKeyIdKey,
            BlobBehaviorTransform.EncryptionIvKey);

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

    private static bool IsEncrypted(BlobInfo info) =>
        string.Equals(
            info?.Properties?.Get<string>(BlobBehaviorTransform.EncryptionAlgorithmKey),
            Algorithm,
            StringComparison.OrdinalIgnoreCase);

    private static Result<byte[]> GetIv(BlobInfo info)
    {
        var value = info?.Properties?.Get<string>(BlobBehaviorTransform.EncryptionIvKey);
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<byte[]>.Failure(new BlobStoreIntegrityError("Encrypted blob metadata does not contain an initialization vector."));
        }

        try
        {
            return Result<byte[]>.Success(Convert.FromBase64String(value));
        }
        catch (FormatException exception)
        {
            return Result<byte[]>.Failure(new BlobStoreIntegrityError($"Encrypted blob initialization vector is invalid: {exception.Message}"));
        }
    }

    private BlobInfo ToLogicalInfo(BlobInfo info, BlobTransformCopyResult copy = null)
    {
        var mapped = BlobBehaviorTransform.ApplyLogicalInfo(
            info,
            BlobBehaviorTransform.EncryptionAlgorithmKey,
            BlobBehaviorTransform.EncryptionLengthKey,
            BlobBehaviorTransform.EncryptionContentTypeKey,
            BlobBehaviorTransform.EncryptionContentHashKey,
            BlobBehaviorTransform.EncryptionKeyIdKey,
            BlobBehaviorTransform.EncryptionIvKey);

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
