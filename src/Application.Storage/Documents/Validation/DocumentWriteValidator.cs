// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Validates optional document version conditions before persistence.</summary>
/// <example><code>var result = DocumentWriteValidator.Validate(new DocumentWriteOptions());</code></example>
public static class DocumentWriteValidator
{
    /// <summary>Validates a content write condition.</summary>
    /// <example><code>var result = DocumentWriteValidator.Validate(options);</code></example>
    public static Result Validate(DocumentWriteOptions options) => ValidateCondition(options?.IfMatchETag, options?.CreateOnly == true);

    /// <summary>Validates a metadata update condition.</summary>
    /// <example><code>var result = DocumentWriteValidator.Validate(update);</code></example>
    public static Result Validate(DocumentPropertiesUpdate update) => ValidateCondition(update?.IfMatchETag);

    /// <summary>Validates an exact-delete condition.</summary>
    /// <example><code>var result = DocumentWriteValidator.Validate(options);</code></example>
    public static Result Validate(DocumentDeleteOptions options) => ValidateCondition(options?.IfMatchETag);

    private static Result ValidateCondition(string etag, bool createOnly = false) =>
        etag?.Trim() == "*" || (createOnly && !string.IsNullOrWhiteSpace(etag))
            ? Result.Failure(new DocumentStoreInvalidQueryError("A concrete ETag cannot be combined with create-only; wildcard ETags are unsupported."))
            : Result.Success();
}
