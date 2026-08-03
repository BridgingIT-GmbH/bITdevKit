// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides provider-neutral helpers for storing text and serialized object content as blobs.
/// </summary>
/// <example>
/// <code>
/// var result = await blobs.UploadTextAsync(new BlobKey("notes", "readme.txt"), "hello");
/// </code>
/// </example>
public static class BlobContentStorageExtensions
{
    /// <summary>
    /// Uploads text content as a blob.
    /// </summary>
    /// <param name="blobClient">The blob client to upload to.</param>
    /// <param name="blobKey">The destination blob key.</param>
    /// <param name="content">The text content to upload.</param>
    /// <param name="options">Optional text upload options.</param>
    /// <param name="cancellationToken">A token to cancel the upload.</param>
    /// <returns>A result containing the uploaded blob metadata.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.UploadTextAsync(new BlobKey("notes", "readme.txt"), "hello");
    /// </code>
    /// </example>
    public static async Task<Result<BlobInfo>> UploadTextAsync(
        this IBlobStoreClient blobClient,
        BlobKey blobKey,
        string content,
        BlobTextUploadOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (blobClient is null)
        {
            return Result<BlobInfo>.Failure(new ArgumentError("Blob client cannot be null."));
        }

        if (content is null)
        {
            return Result<BlobInfo>.Failure(new BlobStoreValidationError("Text content cannot be null."));
        }

        var keyValidation = BlobValidator.Validate(blobKey);
        if (keyValidation.IsFailure)
        {
            return Result<BlobInfo>.Failure(keyValidation);
        }

        options ??= new BlobTextUploadOptions();
        var encodingValidation = ValidateEncoding(options.Encoding);
        if (encodingValidation.IsFailure)
        {
            return Result<BlobInfo>.Failure(encodingValidation);
        }

        var contentTypeValidation = ValidateTextContentType(options.ContentType, options.RejectBinaryContentType, false);
        if (contentTypeValidation.IsFailure)
        {
            return Result<BlobInfo>.Failure(contentTypeValidation);
        }

        await using var stream = new MemoryStream(options.Encoding.GetBytes(content));
        var upload = new BlobUpload
        {
            Key = blobKey,
            Content = stream,
            ContentType = options.ContentType,
            ExpectedContentHash = options.ExpectedContentHash,
            Properties = options.Properties?.Clone() ?? new PropertyBag(),
            OverwriteMode = options.OverwriteMode
        };

        return await blobClient.UploadAsync(upload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a blob and decodes it as text.
    /// </summary>
    /// <param name="blobClient">The blob client to download from.</param>
    /// <param name="blobKey">The source blob key.</param>
    /// <param name="options">Optional text download options.</param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    /// <returns>A result containing the decoded text and blob metadata.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.DownloadTextAsync(new BlobKey("notes", "readme.txt"));
    /// </code>
    /// </example>
    public static async Task<Result<BlobTextContent>> DownloadTextAsync(
        this IBlobStoreClient blobClient,
        BlobKey blobKey,
        BlobTextDownloadOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (blobClient is null)
        {
            return Result<BlobTextContent>.Failure(new ArgumentError("Blob client cannot be null."));
        }

        var keyValidation = BlobValidator.Validate(blobKey);
        if (keyValidation.IsFailure)
        {
            return Result<BlobTextContent>.Failure(keyValidation);
        }

        options ??= new BlobTextDownloadOptions();
        var encodingValidation = ValidateEncoding(options.Encoding);
        if (encodingValidation.IsFailure)
        {
            return Result<BlobTextContent>.Failure(encodingValidation);
        }

        var downloadResult = await blobClient.DownloadAsync(blobKey, cancellationToken).ConfigureAwait(false);
        if (downloadResult.IsFailure)
        {
            return Result<BlobTextContent>.Failure(downloadResult);
        }

        await using var download = downloadResult.Value;
        var contentTypeValidation = ValidateTextContentType(
            download.Info?.ContentType,
            options.RejectBinaryContentType,
            options.RequireTextContentType);
        if (contentTypeValidation.IsFailure)
        {
            return Result<BlobTextContent>.Failure(contentTypeValidation);
        }

        try
        {
            using var reader = new StreamReader(download.Content, options.Encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            return Result<BlobTextContent>.Success(new BlobTextContent
            {
                Info = download.Info,
                Text = text
            });
        }
        catch (OperationCanceledException)
        {
            return Result<BlobTextContent>.Failure(new OperationCancelledError("Operation cancelled during blob text download."));
        }
        catch (Exception ex)
        {
            return Result<BlobTextContent>.Failure(new BlobStoreSerializationError($"Blob text content could not be decoded: {ex.GetFullMessage()}"));
        }
    }

    /// <summary>
    /// Serializes an object and uploads it as a blob.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="blobClient">The blob client to upload to.</param>
    /// <param name="blobKey">The destination blob key.</param>
    /// <param name="instance">The object instance to serialize.</param>
    /// <param name="options">Optional object upload options.</param>
    /// <param name="cancellationToken">A token to cancel the upload.</param>
    /// <returns>A result containing the uploaded blob metadata.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.UploadObjectAsync(new BlobKey("profiles", "user.json"), profile);
    /// </code>
    /// </example>
    public static async Task<Result<BlobInfo>> UploadObjectAsync<T>(
        this IBlobStoreClient blobClient,
        BlobKey blobKey,
        T instance,
        BlobObjectUploadOptions options = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (blobClient is null)
        {
            return Result<BlobInfo>.Failure(new ArgumentError("Blob client cannot be null."));
        }

        if (instance is null)
        {
            return Result<BlobInfo>.Failure(new BlobStoreValidationError("Object instance cannot be null."));
        }

        var keyValidation = BlobValidator.Validate(blobKey);
        if (keyValidation.IsFailure)
        {
            return Result<BlobInfo>.Failure(keyValidation);
        }

        options ??= new BlobObjectUploadOptions();
        var contentTypeValidation = ValidateTextContentType(options.ContentType, options.RejectBinaryContentType, false);
        if (contentTypeValidation.IsFailure)
        {
            return Result<BlobInfo>.Failure(contentTypeValidation);
        }

        var serializer = options.Serializer ?? new SystemTextJsonSerializer();

        await using var stream = new MemoryStream();
        try
        {
            serializer.Serialize(instance, stream);
            stream.Position = 0;
        }
        catch (OperationCanceledException)
        {
            return Result<BlobInfo>.Failure(new OperationCancelledError("Operation cancelled during blob object upload."));
        }
        catch (Exception ex)
        {
            return Result<BlobInfo>.Failure(new BlobStoreSerializationError($"Blob object content could not be serialized: {ex.GetFullMessage()}"));
        }

        var upload = new BlobUpload
        {
            Key = blobKey,
            Content = stream,
            ContentType = options.ContentType,
            ExpectedContentHash = options.ExpectedContentHash,
            Properties = options.Properties?.Clone() ?? new PropertyBag(),
            OverwriteMode = options.OverwriteMode
        };

        return await blobClient.UploadAsync(upload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a blob and deserializes it as an object.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="blobClient">The blob client to download from.</param>
    /// <param name="blobKey">The source blob key.</param>
    /// <param name="options">Optional object download options.</param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    /// <returns>A result containing the deserialized value and blob metadata.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.DownloadObjectAsync&lt;Profile&gt;(new BlobKey("profiles", "user.json"));
    /// </code>
    /// </example>
    public static async Task<Result<BlobObjectContent<T>>> DownloadObjectAsync<T>(
        this IBlobStoreClient blobClient,
        BlobKey blobKey,
        BlobObjectDownloadOptions options = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (blobClient is null)
        {
            return Result<BlobObjectContent<T>>.Failure(new ArgumentError("Blob client cannot be null."));
        }

        var keyValidation = BlobValidator.Validate(blobKey);
        if (keyValidation.IsFailure)
        {
            return Result<BlobObjectContent<T>>.Failure(keyValidation);
        }

        options ??= new BlobObjectDownloadOptions();
        var downloadResult = await blobClient.DownloadAsync(blobKey, cancellationToken).ConfigureAwait(false);
        if (downloadResult.IsFailure)
        {
            return Result<BlobObjectContent<T>>.Failure(downloadResult);
        }

        await using var download = downloadResult.Value;
        var contentTypeValidation = ValidateTextContentType(
            download.Info?.ContentType,
            options.RejectBinaryContentType,
            options.RequireTextContentType);
        if (contentTypeValidation.IsFailure)
        {
            return Result<BlobObjectContent<T>>.Failure(contentTypeValidation);
        }

        var serializer = options.Serializer ?? new SystemTextJsonSerializer();

        try
        {
            var value = serializer.Deserialize<T>(download.Content);
            if (value is null)
            {
                return Result<BlobObjectContent<T>>.Failure(new BlobStoreSerializationError("Blob object content deserialized to null."));
            }

            return Result<BlobObjectContent<T>>.Success(new BlobObjectContent<T>
            {
                Info = download.Info,
                Value = value
            });
        }
        catch (OperationCanceledException)
        {
            return Result<BlobObjectContent<T>>.Failure(new OperationCancelledError("Operation cancelled during blob object download."));
        }
        catch (Exception ex)
        {
            return Result<BlobObjectContent<T>>.Failure(new BlobStoreSerializationError($"Blob object content could not be deserialized: {ex.GetFullMessage()}"));
        }
    }

    private static Result ValidateEncoding(System.Text.Encoding encoding)
    {
        return encoding is null
            ? Result.Failure(new BlobStoreValidationError("Text encoding cannot be null."))
            : Result.Success();
    }

    private static Result ValidateTextContentType(ContentType? contentType, bool rejectBinary, bool requireText)
    {
        if (contentType is null)
        {
            return requireText
                ? Result.Failure(new BlobStoreValidationError("Blob content type is required for this text operation."))
                : Result.Success();
        }

        if (rejectBinary && contentType.Value.IsBinary())
        {
            return Result.Failure(new BlobStoreValidationError($"Content type '{contentType.Value}' is binary and cannot be used with text blob helpers."));
        }

        if (requireText && !contentType.Value.IsText())
        {
            return Result.Failure(new BlobStoreValidationError($"Content type '{contentType.Value}' is not marked as text."));
        }

        return Result.Success();
    }
}
