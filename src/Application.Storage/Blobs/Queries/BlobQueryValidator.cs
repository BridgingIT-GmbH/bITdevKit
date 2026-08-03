// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Validates provider-agnostic blob query rules.
/// </summary>
/// <example>
/// <code>
/// var validation = BlobQueryValidator.NormalizeAndValidate("inmemory", query, options, capabilities);
/// </code>
/// </example>
public static class BlobQueryValidator
{
    /// <summary>
    /// Normalizes and validates a blob listing query.
    /// </summary>
    /// <param name="provider">The provider discriminator expected in continuation tokens.</param>
    /// <param name="query">The query to normalize and validate.</param>
    /// <param name="options">The blob-store options used to constrain the query.</param>
    /// <param name="capabilities">The provider capabilities used to validate supported query shapes.</param>
    /// <param name="protector">The optional continuation-token protector.</param>
    /// <returns>A result containing normalized query values, or validation errors.</returns>
    /// <example>
    /// <code>
    /// var validation = BlobQueryValidator.NormalizeAndValidate("azure-blob", query, options, capabilities);
    /// </code>
    /// </example>
    public static Result<BlobQueryValidation> NormalizeAndValidate(
        string provider,
        BlobQuery query,
        BlobStoreOptions options,
        BlobStoreProviderCapabilities capabilities,
        IContinuationTokenProtector protector = null)
    {
        if (query is null)
        {
            return Result<BlobQueryValidation>.Failure(new BlobStoreValidationError("Blob query is required."));
        }

        options ??= new BlobStoreOptions();
        capabilities ??= new BlobStoreProviderCapabilities();

        var optionsResult = options.Validate();
        if (optionsResult.IsFailure)
        {
            return Result<BlobQueryValidation>.Failure(optionsResult);
        }

        if (!capabilities.SupportsContinuationPaging)
        {
            return Result<BlobQueryValidation>.Failure(new BlobStoreQueryNotSupportedError("Provider does not support continuation paging."));
        }

        if (string.IsNullOrWhiteSpace(query.Container))
        {
            return Result<BlobQueryValidation>.Failure(new BlobStoreValidationError("Blob query container is required."));
        }

        var take = query.Take ?? options.DefaultTake;
        if (take <= 0)
        {
            return Result<BlobQueryValidation>.Failure(new BlobStoreValidationError("Take must be greater than zero."));
        }

        if (take > options.MaxTake)
        {
            return Result<BlobQueryValidation>.Failure(new BlobStorePageSizeExceededError(take, options.MaxTake));
        }

        var shapeResult = ValidateShape(query, options, capabilities);
        if (shapeResult.IsFailure)
        {
            return Result<BlobQueryValidation>.Failure(shapeResult);
        }

        var normalizedQuery = new BlobQuery
        {
            Container = query.Container,
            Prefix = query.Prefix,
            Take = take,
            ContinuationToken = query.ContinuationToken,
            AllowFullScan = query.AllowFullScan
        };
        var queryHash = BlobQueryHash.Compute(normalizedQuery);
        BlobContinuationToken continuation = null;
        if (!string.IsNullOrWhiteSpace(query.ContinuationToken))
        {
            var tokenResult = BlobContinuationTokenSerializer.Deserialize(query.ContinuationToken, protector);
            if (tokenResult.IsFailure)
            {
                return Result<BlobQueryValidation>.Failure(tokenResult);
            }

            continuation = tokenResult.Value;
            if (!string.Equals(continuation.Provider, provider, StringComparison.Ordinal))
            {
                return Result<BlobQueryValidation>.Failure(new BlobStoreInvalidContinuationTokenError("Continuation token provider does not match this provider."));
            }

            if (!string.Equals(continuation.QueryHash, queryHash, StringComparison.Ordinal))
            {
                return Result<BlobQueryValidation>.Failure(new BlobStoreInvalidContinuationTokenError("Continuation token does not match the query."));
            }
        }

        return Result<BlobQueryValidation>.Success(new BlobQueryValidation(normalizedQuery, take, queryHash, continuation));
    }

    private static Result ValidateShape(
        BlobQuery query,
        BlobStoreOptions options,
        BlobStoreProviderCapabilities capabilities)
    {
        if (string.IsNullOrEmpty(query.Prefix))
        {
            if (options.RequireExplicitFullScanApproval && !query.AllowFullScan)
            {
                return Result.Failure(new BlobStoreQueryTooBroadError("Full container scans require explicit query approval. Set AllowFullScan=true, pass --full-scan in console commands, or provide a prefix."));
            }

            if (!options.AllowFullScans)
            {
                return Result.Failure(new BlobStoreQueryTooBroadError("Full container scans are disabled for this blob client. Enable AllowFullScans in the client options or provide a prefix."));
            }

            return capabilities.SupportsFullContainerScan
                ? Result.Success()
                : Result.Failure(new BlobStoreQueryNotSupportedError("Provider does not support full container scans."));
        }

        return capabilities.SupportsPrefixListing
            ? Result.Success()
            : Result.Failure(new BlobStoreQueryNotSupportedError("Provider does not support prefix listing."));
    }
}

/// <summary>
/// Represents normalized blob query validation output.
/// </summary>
/// <param name="Query">The normalized query model carrying the resolved page size.</param>
/// <param name="Take">The normalized page size to use for the query.</param>
/// <param name="QueryHash">The stable hash for the logical query shape.</param>
/// <param name="ContinuationToken">The validated continuation token envelope, or null when the query starts a new page sequence.</param>
public sealed record BlobQueryValidation(
    BlobQuery Query,
    int Take,
    string QueryHash,
    BlobContinuationToken ContinuationToken);
