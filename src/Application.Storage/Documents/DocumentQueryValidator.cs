// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Validates provider-agnostic document query rules.
/// </summary>
/// <example>
/// <code>
/// var validation = DocumentQueryValidator.ValidatePage&lt;Person&gt;(
///     "find",
///     "cosmos",
///     query,
///     provider.Capabilities,
///     options);
/// </code>
/// </example>
public static class DocumentQueryValidator
{
    /// <summary>
    /// Validates a page query and returns normalized values.
    /// </summary>
    /// <typeparam name="T">The document payload type used by the query.</typeparam>
    /// <param name="operation">The logical page operation name, such as <c>find</c> or <c>list</c>.</param>
    /// <param name="provider">The provider name expected in continuation tokens.</param>
    /// <param name="query">The page query to validate.</param>
    /// <param name="capabilities">The provider capabilities used to decide whether the query shape is supported.</param>
    /// <param name="options">The document-store options used to normalize and constrain the query.</param>
    /// <returns>A result containing normalized query values, or validation errors.</returns>
    /// <example>
    /// <code>
    /// var validation = DocumentQueryValidator.ValidatePage&lt;Person&gt;(
    ///     "find",
    ///     "cosmos",
    ///     query,
    ///     capabilities,
    ///     options);
    /// </code>
    /// </example>
    public static Result<DocumentQueryValidation> ValidatePage<T>(
        string operation,
        string provider,
        DocumentQuery query,
        DocumentStoreProviderCapabilities capabilities,
        DocumentStoreOptions options)
        where T : class, new()
    {
        query ??= new DocumentQuery();
        capabilities ??= new DocumentStoreProviderCapabilities();
        options ??= new DocumentStoreOptions();

        var optionsResult = options.Validate();
        if (optionsResult.IsFailure)
        {
            return Result<DocumentQueryValidation>.Failure(optionsResult);
        }

        if (!capabilities.SupportsContinuationPaging)
        {
            return Result<DocumentQueryValidation>.Failure(new DocumentStoreQueryNotSupportedError("Provider does not support continuation paging."));
        }

        var take = query.Take ?? options.DefaultTake;
        if (take <= 0)
        {
            return Result<DocumentQueryValidation>.Failure(new DocumentStoreInvalidQueryError("Take must be greater than zero."));
        }

        if (take > options.MaxTake)
        {
            return Result<DocumentQueryValidation>.Failure(new DocumentStorePageSizeExceededError($"Take cannot exceed {options.MaxTake}."));
        }

        var supportResult = ValidateShape(query.DocumentKey, query.Filter, query.AllowFullScan, capabilities, options);
        if (supportResult.IsFailure)
        {
            return Result<DocumentQueryValidation>.Failure(supportResult);
        }

        var queryHash = DocumentQueryHash.Compute<T>(operation, query, take);
        DocumentContinuationToken continuation = null;
        if (!string.IsNullOrWhiteSpace(query.ContinuationToken))
        {
            var tokenResult = DocumentContinuationTokenSerializer.Deserialize(query.ContinuationToken);
            if (tokenResult.IsFailure)
            {
                return Result<DocumentQueryValidation>.Failure(tokenResult);
            }

            continuation = tokenResult.Value;
            if (!string.Equals(continuation.Provider, provider, StringComparison.Ordinal))
            {
                return Result<DocumentQueryValidation>.Failure(new DocumentStoreInvalidContinuationTokenError("Continuation token provider does not match this provider."));
            }

            if (!string.Equals(continuation.QueryHash, queryHash, StringComparison.Ordinal))
            {
                return Result<DocumentQueryValidation>.Failure(new DocumentStoreContinuationTokenQueryMismatchError("Continuation token does not match the query."));
            }
        }

        return Result<DocumentQueryValidation>.Success(new DocumentQueryValidation(take, queryHash, continuation));
    }

    /// <summary>
    /// Validates a count query.
    /// </summary>
    /// <typeparam name="T">The document payload type used by the query.</typeparam>
    /// <param name="operation">The logical count operation name.</param>
    /// <param name="query">The count query to validate.</param>
    /// <param name="capabilities">The provider capabilities used to decide whether the query shape is supported.</param>
    /// <param name="options">The document-store options used to constrain the query.</param>
    /// <returns>A result containing normalized count query values, or validation errors.</returns>
    /// <example>
    /// <code>
    /// var validation = DocumentQueryValidator.ValidateCount&lt;Person&gt;(
    ///     "count",
    ///     query,
    ///     capabilities,
    ///     options);
    /// </code>
    /// </example>
    public static Result<DocumentCountQueryValidation> ValidateCount<T>(
        string operation,
        DocumentCountQuery query,
        DocumentStoreProviderCapabilities capabilities,
        DocumentStoreOptions options)
        where T : class, new()
    {
        query ??= new DocumentCountQuery();
        capabilities ??= new DocumentStoreProviderCapabilities();
        options ??= new DocumentStoreOptions();

        var optionsResult = options.Validate();
        if (optionsResult.IsFailure)
        {
            return Result<DocumentCountQueryValidation>.Failure(optionsResult);
        }

        var supportResult = ValidateShape(query.DocumentKey, query.Filter, query.AllowFullScan, capabilities, options);
        if (supportResult.IsFailure)
        {
            return Result<DocumentCountQueryValidation>.Failure(supportResult);
        }

        return Result<DocumentCountQueryValidation>.Success(new DocumentCountQueryValidation(DocumentQueryHash.Compute<T>(operation, query)));
    }

    private static Result ValidateShape(
        DocumentKey? documentKey,
        DocumentKeyFilter filter,
        bool allowFullScan,
        DocumentStoreProviderCapabilities capabilities,
        DocumentStoreOptions options)
    {
        if (documentKey is null)
        {
            if (!allowFullScan)
            {
                return Result.Failure(new DocumentStoreFullScanNotAllowedError("Full scans require query AllowFullScan."));
            }

            if (!options.AllowFullScans)
            {
                return Result.Failure(new DocumentStoreFullScanNotAllowedError("Full scans are disabled by options."));
            }

            return ValidateSupport(capabilities.FullScan, options, "Full scan");
        }

        if (string.IsNullOrWhiteSpace(documentKey.Value.PartitionKey))
        {
            return Result.Failure(new DocumentStoreInvalidQueryError("PartitionKey must not be null or whitespace."));
        }

        if (filter == DocumentKeyFilter.FullMatch && string.IsNullOrWhiteSpace(documentKey.Value.RowKey))
        {
            return Result.Failure(new DocumentStoreInvalidQueryError("RowKey must not be null or whitespace for full match queries."));
        }

        if (filter is DocumentKeyFilter.RowKeyPrefixMatch or DocumentKeyFilter.RowKeySuffixMatch &&
            documentKey.Value.RowKey is null)
        {
            return Result.Failure(new DocumentStoreInvalidQueryError("RowKey must not be null for row-key filter queries."));
        }

        return filter switch
        {
            DocumentKeyFilter.FullMatch => ValidateSupport(capabilities.FullMatch, options, "Full match"),
            DocumentKeyFilter.RowKeyPrefixMatch => ValidateSupport(capabilities.RowKeyPrefixMatch, options, "Row-key prefix match"),
            DocumentKeyFilter.RowKeySuffixMatch => ValidateSupport(capabilities.RowKeySuffixMatch, options, "Row-key suffix match"),
            _ => Result.Failure(new DocumentStoreQueryNotSupportedError($"Document key filter '{filter}' is not supported."))
        };
    }

    private static Result ValidateSupport(DocumentQuerySupport support, DocumentStoreOptions options, string description)
    {
        if (support == DocumentQuerySupport.Unsupported)
        {
            return Result.Failure(new DocumentStoreQueryNotSupportedError($"{description} is not supported by this provider."));
        }

        if (support == DocumentQuerySupport.SupportedClientSide && options.RejectClientSideFilteredQueries)
        {
            return Result.Failure(new DocumentStoreClientSideFilteringRejectedError($"{description} requires client-side filtering."));
        }

        return Result.Success();
    }
}

/// <summary>
/// Represents normalized page-query validation output.
/// </summary>
/// <param name="Take">The normalized page size to use for the query.</param>
/// <param name="QueryHash">The stable hash for the validated query shape.</param>
/// <param name="ContinuationToken">The validated continuation token envelope, or null when the query starts a new page sequence.</param>
public sealed record DocumentQueryValidation(int Take, string QueryHash, DocumentContinuationToken ContinuationToken);

/// <summary>
/// Represents normalized count-query validation output.
/// </summary>
/// <param name="QueryHash">The stable hash for the validated count query shape.</param>
public sealed record DocumentCountQueryValidation(string QueryHash);
