// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Security.Cryptography;

/// <summary>
/// Provides shared stream and metadata helpers for blob-store behaviors.
/// </summary>
/// <example>
/// <code>
/// using var temp = BlobBehaviorTransform.CreateTemporaryStream();
/// var result = await BlobBehaviorTransform.CopyAndHashAsync(source, temp, cancellationToken);
/// </code>
/// </example>
public static class BlobBehaviorTransform
{
    /// <summary>
    /// Gets the property key used to store the compression algorithm.
    /// </summary>
    public const string CompressionAlgorithmKey = "bdk_compression_algorithm";
    /// <summary>
    /// Gets the property key used to store the uncompressed content length.
    /// </summary>
    public const string CompressionLengthKey = "bdk_compression_length";
    /// <summary>
    /// Gets the property key used to store the uncompressed content type.
    /// </summary>
    public const string CompressionContentTypeKey = "bdk_compression_content_type";
    /// <summary>
    /// Gets the property key used to store the uncompressed content hash.
    /// </summary>
    public const string CompressionContentHashKey = "bdk_compression_content_hash";

    /// <summary>
    /// Gets the property key used to store the encryption algorithm.
    /// </summary>
    public const string EncryptionAlgorithmKey = "bdk_encryption_algorithm";
    /// <summary>
    /// Gets the property key used to store the decrypted content length.
    /// </summary>
    public const string EncryptionLengthKey = "bdk_encryption_length";
    /// <summary>
    /// Gets the property key used to store the decrypted content type.
    /// </summary>
    public const string EncryptionContentTypeKey = "bdk_encryption_content_type";
    /// <summary>
    /// Gets the property key used to store the decrypted content hash.
    /// </summary>
    public const string EncryptionContentHashKey = "bdk_encryption_content_hash";
    /// <summary>
    /// Gets the property key used to store the encryption key identifier.
    /// </summary>
    public const string EncryptionKeyIdKey = "bdk_encryption_key_id";
    /// <summary>
    /// Gets the property key used to store the encryption initialization vector.
    /// </summary>
    public const string EncryptionIvKey = "bdk_encryption_iv";

    private const int BufferSize = 81920;

    /// <summary>
    /// Creates a temporary seekable stream for transformed blob content.
    /// </summary>
    /// <returns>A temporary file stream that deletes itself when closed.</returns>
    /// <example>
    /// <code>
    /// await using var temp = BlobBehaviorTransform.CreateTemporaryStream();
    /// </code>
    /// </example>
    public static FileStream CreateTemporaryStream()
        => TemporaryFileHelper.Create(prefix: "bdk-blob-").Stream;

    /// <summary>
    /// Copies a source stream to a destination stream while calculating a blob content hash.
    /// </summary>
    /// <param name="source">The source stream to read.</param>
    /// <param name="destination">The destination stream to write.</param>
    /// <param name="cancellationToken">A token to cancel the copy.</param>
    /// <returns>The copied length and calculated content hash.</returns>
    /// <example>
    /// <code>
    /// var copy = await BlobBehaviorTransform.CopyAndHashAsync(source, destination, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<BlobTransformCopyResult> CopyAndHashAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var result = await StreamHelper.CopyAsync(
                source,
                destination,
                new StreamCopyOptions { BufferSize = BufferSize, HashAlgorithm = HashAlgorithmName.SHA256 },
                cancellationToken)
            .ConfigureAwait(false);
        return new BlobTransformCopyResult(result.Length, $"{BlobContentHash.Prefix}{result.Hash}");
    }

    /// <summary>
    /// Validates that blob content is readable.
    /// </summary>
    /// <param name="content">The content stream to validate.</param>
    /// <returns>A success result when the stream is readable; otherwise a validation failure.</returns>
    /// <example>
    /// <code>
    /// var result = BlobBehaviorTransform.ValidateContent(upload.Content);
    /// </code>
    /// </example>
    public static Result ValidateContent(Stream content)
    {
        if (content is null)
        {
            return Result.Failure(new BlobStoreValidationError("Blob content stream is required."));
        }

        if (!content.CanRead)
        {
            return Result.Failure(new BlobStoreValidationError("Blob content stream must be readable."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Validates an expected content hash against an actual content hash.
    /// </summary>
    /// <param name="expectedContentHash">The optional expected content hash.</param>
    /// <param name="actualContentHash">The actual calculated content hash.</param>
    /// <returns>A success result when the hashes match or no expected hash is supplied.</returns>
    /// <example>
    /// <code>
    /// var result = BlobBehaviorTransform.ValidateExpectedHash(expectedHash, actualHash);
    /// </code>
    /// </example>
    public static Result ValidateExpectedHash(string expectedContentHash, string actualContentHash)
    {
        var format = BlobContentHash.ValidateExpectedHash(expectedContentHash);
        if (format.IsFailure)
        {
            return format;
        }

        return string.IsNullOrWhiteSpace(expectedContentHash) ||
            string.Equals(expectedContentHash, actualContentHash, StringComparison.Ordinal)
                ? Result.Success()
                : Result.Failure(new BlobStoreIntegrityError("ExpectedContentHash does not match uploaded content."));
    }

    /// <summary>
    /// Clones a blob property bag.
    /// </summary>
    /// <param name="properties">The property bag to clone.</param>
    /// <returns>A cloned property bag, or an empty bag when none was supplied.</returns>
    /// <example>
    /// <code>
    /// var properties = BlobBehaviorTransform.CloneProperties(info.Properties);
    /// </code>
    /// </example>
    public static PropertyBag CloneProperties(PropertyBag properties) => properties?.Clone() ?? new PropertyBag();

    /// <summary>
    /// Resolves a content type from a MIME type string.
    /// </summary>
    /// <param name="mimeType">The MIME type string.</param>
    /// <returns>The resolved content type, or <c>null</c> when the MIME type is empty.</returns>
    /// <example>
    /// <code>
    /// var contentType = BlobBehaviorTransform.ContentTypeFromMimeType("text/plain");
    /// </code>
    /// </example>
    public static ContentType? ContentTypeFromMimeType(string mimeType) =>
        string.IsNullOrWhiteSpace(mimeType)
            ? null
            : Enum.TryParse<ContentType>(mimeType, ignoreCase: true, out var contentType)
                ? contentType
                : ContentTypeExtensions.FromMimeType(mimeType, ContentType.DEFAULT);

    /// <summary>
    /// Maps provider metadata for transformed content back to logical blob metadata.
    /// </summary>
    /// <param name="info">The provider blob metadata.</param>
    /// <param name="algorithmKey">The transformation algorithm property key.</param>
    /// <param name="lengthKey">The logical length property key.</param>
    /// <param name="contentTypeKey">The logical content type property key.</param>
    /// <param name="contentHashKey">The logical content hash property key.</param>
    /// <param name="additionalInternalKeys">Additional transformation metadata keys to remove.</param>
    /// <returns>The logical blob metadata.</returns>
    /// <example>
    /// <code>
    /// var logical = BlobBehaviorTransform.ApplyLogicalInfo(info, algorithmKey, lengthKey, contentTypeKey, contentHashKey);
    /// </code>
    /// </example>
    public static BlobInfo ApplyLogicalInfo(
        BlobInfo info,
        string algorithmKey,
        string lengthKey,
        string contentTypeKey,
        string contentHashKey,
        params string[] additionalInternalKeys)
    {
        if (info is null)
        {
            return null;
        }

        var properties = CloneProperties(info.Properties);
        var isTransformed = properties.Contains(algorithmKey);
        var length = info.Length;
        var contentType = info.ContentType;
        var contentHash = info.ContentHash;

        if (isTransformed)
        {
            var storedLength = properties.Get<string>(lengthKey);
            if (long.TryParse(storedLength, out var logicalLength))
            {
                length = logicalLength;
            }

            contentType = ContentTypeFromMimeType(properties.Get<string>(contentTypeKey));
            contentHash = properties.Get<string>(contentHashKey, info.ContentHash);
        }

        properties.Remove(algorithmKey);
        properties.Remove(lengthKey);
        properties.Remove(contentTypeKey);
        properties.Remove(contentHashKey);

        foreach (var key in additionalInternalKeys)
        {
            properties.Remove(key);
        }

        return new BlobInfo
        {
            Key = info.Key,
            Length = length,
            ContentType = contentType,
            ContentHash = contentHash,
            ETag = info.ETag,
            CreatedAt = info.CreatedAt,
            LastModifiedAt = info.LastModifiedAt,
            ExpiresAt = info.ExpiresAt,
            Properties = properties
        };
    }

    /// <summary>
    /// Maps all items in a blob page from provider metadata to logical metadata.
    /// </summary>
    /// <param name="page">The provider blob page.</param>
    /// <param name="mapInfo">The metadata mapping function.</param>
    /// <returns>The mapped blob page.</returns>
    /// <example>
    /// <code>
    /// var logicalPage = BlobBehaviorTransform.ApplyLogicalPage(page, MapInfo);
    /// </code>
    /// </example>
    public static BlobPage ApplyLogicalPage(
        BlobPage page,
        Func<BlobInfo, BlobInfo> mapInfo)
    {
        if (page is null)
        {
            return null;
        }

        return new BlobPage
        {
            Items = page.Items?.Select(mapInfo).ToArray() ?? [],
            ContinuationToken = page.ContinuationToken
        };
    }

    /// <summary>
    /// Preserves transformation metadata during logical property updates.
    /// </summary>
    /// <param name="update">The caller-supplied property update.</param>
    /// <param name="current">The current logical blob metadata.</param>
    /// <param name="algorithmKey">The transformation algorithm property key.</param>
    /// <param name="lengthKey">The logical length property key.</param>
    /// <param name="contentTypeKey">The logical content type property key.</param>
    /// <param name="contentHashKey">The logical content hash property key.</param>
    /// <param name="additionalInternalKeys">Additional transformation metadata keys to preserve.</param>
    /// <returns>A property update that preserves transformation metadata.</returns>
    /// <example>
    /// <code>
    /// var innerUpdate = BlobBehaviorTransform.PreserveInternalProperties(update, current, algorithmKey, lengthKey, contentTypeKey, contentHashKey);
    /// </code>
    /// </example>
    public static BlobPropertiesUpdate PreserveInternalProperties(
        BlobPropertiesUpdate update,
        BlobInfo current,
        string algorithmKey,
        string lengthKey,
        string contentTypeKey,
        string contentHashKey,
        params string[] additionalInternalKeys)
    {
        var currentProperties = current?.Properties;
        if (currentProperties is null || !currentProperties.Contains(algorithmKey))
        {
            return update;
        }

        var properties = CloneProperties(update.Properties);
        properties[algorithmKey] = currentProperties.Get<string>(algorithmKey);
        properties[lengthKey] = currentProperties.Get<string>(lengthKey);
        properties[contentHashKey] = currentProperties.Get<string>(contentHashKey);
        properties[contentTypeKey] = update.ContentType?.ToString() ?? currentProperties.Get<string>(contentTypeKey);

        foreach (var key in additionalInternalKeys)
        {
            if (currentProperties.TryGetValue(key, out var value))
            {
                properties[key] = value;
            }
        }

        return new BlobPropertiesUpdate
        {
            Key = update.Key,
            ContentType = current.ContentType,
            ExpiresAt = update.ExpiresAt,
            Properties = properties,
            IfMatchETag = update.IfMatchETag
        };
    }
}

/// <summary>
/// Describes a completed behavior transform copy.
/// </summary>
/// <param name="Length">The number of copied bytes.</param>
/// <param name="ContentHash">The calculated blob content hash.</param>
/// <example>
/// <code>
/// var length = result.Length;
/// var hash = result.ContentHash;
/// </code>
/// </example>
public sealed record BlobTransformCopyResult(long Length, string ContentHash);
