// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Validates provider-neutral blob operation models.
/// </summary>
/// <example>
/// <code>
/// var validation = BlobValidator.Validate(upload, options);
/// </code>
/// </example>
public static class BlobValidator
{
    /// <summary>
    /// Validates a blob key.
    /// </summary>
    /// <param name="key">The blob key to validate.</param>
    /// <returns>A success result when the key has required values.</returns>
    /// <example>
    /// <code>
    /// var validation = BlobValidator.Validate(new BlobKey("reports", "2026/06/report.pdf"));
    /// </code>
    /// </example>
    public static Result Validate(BlobKey key)
    {
        if (key is null)
        {
            return Result.Failure(new BlobStoreValidationError("Blob key is required."));
        }

        if (string.IsNullOrWhiteSpace(key.Container))
        {
            return Result.Failure(new BlobStoreValidationError("Blob key container is required."));
        }

        if (string.IsNullOrWhiteSpace(key.Name))
        {
            return Result.Failure(new BlobStoreValidationError("Blob key name is required."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Validates a blob upload model before provider execution.
    /// </summary>
    /// <param name="upload">The upload model to validate.</param>
    /// <param name="options">The blob store options used for size checks.</param>
    /// <returns>A success result when the upload is valid.</returns>
    /// <example>
    /// <code>
    /// var validation = BlobValidator.Validate(upload, options);
    /// </code>
    /// </example>
    public static Result Validate(BlobUpload upload, BlobStoreOptions options = null)
    {
        options ??= new BlobStoreOptions();

        var optionsResult = options.Validate();
        if (optionsResult.IsFailure)
        {
            return optionsResult;
        }

        if (upload is null)
        {
            return Result.Failure(new BlobStoreValidationError("Blob upload is required."));
        }

        var keyResult = Validate(upload.Key);
        if (keyResult.IsFailure)
        {
            return keyResult;
        }

        if (upload.Content is null)
        {
            return Result.Failure(new BlobStoreValidationError("Blob upload content stream is required."));
        }

        if (!upload.Content.CanRead)
        {
            return Result.Failure(new BlobStoreValidationError("Blob upload content stream must be readable."));
        }

        if (!Enum.IsDefined(typeof(BlobOverwriteMode), upload.OverwriteMode))
        {
            return Result.Failure(new BlobStoreValidationError("Blob overwrite mode is invalid."));
        }

        if (upload.Properties is null)
        {
            return Result.Failure(new BlobStoreValidationError("Blob upload properties are required."));
        }

        var contentTypeResult = ValidateContentType(upload.ContentType);
        if (contentTypeResult.IsFailure)
        {
            return contentTypeResult;
        }

        var hashResult = BlobContentHash.ValidateExpectedHash(upload.ExpectedContentHash);
        if (hashResult.IsFailure)
        {
            return hashResult;
        }

        return BlobSizeLimit.ValidateKnownLength(upload.Content, options.MaxBlobSize);
    }

    /// <summary>
    /// Validates a blob properties update model before provider execution.
    /// </summary>
    /// <param name="update">The properties update model to validate.</param>
    /// <returns>A success result when the update is valid.</returns>
    /// <example>
    /// <code>
    /// var validation = BlobValidator.Validate(update);
    /// </code>
    /// </example>
    public static Result Validate(BlobPropertiesUpdate update)
    {
        if (update is null)
        {
            return Result.Failure(new BlobStoreValidationError("Blob properties update is required."));
        }

        var keyResult = Validate(update.Key);
        if (keyResult.IsFailure)
        {
            return keyResult;
        }

        if (update.Properties is null)
        {
            return Result.Failure(new BlobStoreValidationError("Blob properties are required."));
        }

        return ValidateContentType(update.ContentType);
    }

    private static Result ValidateContentType(ContentType? contentType)
    {
        if (contentType is null)
        {
            return Result.Success();
        }

        return string.IsNullOrWhiteSpace(contentType.Value.MimeType())
            ? Result.Failure(new BlobStoreValidationError("ContentType must map to a MIME type."))
            : Result.Success();
    }
}
