// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>Sets the mandatory storage boundaries for a blob filter query.</summary>
/// <param name="Container">The container shared by every query branch.</param>
/// <param name="Prefix">An optional name prefix applied to every branch.</param>
/// <param name="AllowFullScan">Whether this request approves a full scan when the client permits one.</param>
/// <example><code>var scope = new BlobFilterScope("reports");</code></example>
public sealed record BlobFilterScope(string Container, string Prefix = null, bool AllowFullScan = false);

/// <summary>Represents a validated blob filter query passed to a storage provider.</summary>
/// <example><code>var criteria = query.Criteria;</code></example>
public sealed class BlobFilterQuery
{
    private BlobFilterQuery(BlobFilterScope scope, StorageFilterCriterion<BlobDimensionFilter> criteria,
        IReadOnlyList<StorageFilterOrder> order, string[] position)
    {
        this.Scope = scope;
        this.Criteria = criteria;
        this.Order = order;
        this.Position = position is null ? null : Array.AsReadOnly(position);
    }

    /// <summary>Gets the scope that applies to every filter branch.</summary>
    public BlobFilterScope Scope { get; }

    /// <summary>Gets the validated predicate tree.</summary>
    public StorageFilterCriterion<BlobDimensionFilter> Criteria { get; }

    /// <summary>Gets the complete seek order, including tie-breakers.</summary>
    public IReadOnlyList<StorageFilterOrder> Order { get; }

    /// <summary>Gets the cursor position in seek order, or null for the first page.</summary>
    public IReadOnlyList<string> Position { get; }

    /// <summary>Identifies these continuation tokens separately from native listing tokens.</summary>
    public const string Purpose = "blob-filter-search";

    /// <summary>Validates a request and binds any continuation to its normalized filter, scope, and ordering.</summary>
    public static Result<(BlobFilterQuery Query, int PageSize, string Hash)> Prepare(
        BlobFilterScope scope, FilterModel filter, string token, string providerName, string clientName,
        BlobStoreOptions options, BlobStoreProviderCapabilities capabilities, IContinuationTokenProtector protector)
    {
        if (capabilities?.SupportsFilterModel != true)
            return Result<(BlobFilterQuery, int, string)>.Failure(new BlobStoreQueryNotSupportedError("Provider does not support FilterModel queries."));

        options ??= new();
        var optionsResult = options.Validate();
        if (optionsResult.IsFailure) return Result<(BlobFilterQuery, int, string)>.Failure(optionsResult);

        try
        {
            if (scope is null || !StorageFilterQuery.IsKey(scope.Container, 256) ||
                scope.Prefix is not null && !StorageFilterQuery.IsKey(scope.Prefix, 2048))
                throw new ArgumentException("Invalid blob container or prefix.");

            var size = StorageFilterQuery.PageSize(filter, options.DefaultTake, options.MaxTake);
            var order = StorageFilterQuery.Order(filter, ["Name"], "NameHash");
            var criteria = StorageFilterQuery.Parse<BlobDimensionFilter>(filter, ParseLeaf);
            var shape = scope.Prefix is null && criteria?.IsAnchored == true
                ? Result.Success()
                : BlobQueryValidator.ValidateShape(new BlobQuery { Prefix = scope.Prefix, AllowFullScan = scope.AllowFullScan }, options, capabilities);
            if (shape.IsFailure) return Result<(BlobFilterQuery, int, string)>.Failure(shape);

            var hash = StorageFilterQuery.Hash(scope, size, order, criteria, dimension => dimension.ToCanonicalString());
            string[] position = null;
            if (token is not null)
            {
                position = StorageFilterQuery.ReadPosition(token, Purpose, providerName, clientName, null, hash, protector);
                if (!IsValidPosition(position))
                    throw new FormatException("Invalid continuation position.");
            }

            return Result<(BlobFilterQuery, int, string)>.Success((new(scope, criteria, order, position), size, hash));
        }
        catch (FormatException)
        {
            return Result<(BlobFilterQuery, int, string)>.Failure(new BlobStoreInvalidContinuationTokenError("Invalid FilterModel continuation token."));
        }
        catch (ArgumentException exception)
        {
            return Result<(BlobFilterQuery, int, string)>.Failure(new ValidationError(exception.Message));
        }
        catch (JsonException)
        {
            return Result<(BlobFilterQuery, int, string)>.Failure(new ValidationError("Invalid typed Dimension operand."));
        }

        StorageFilterCriterion<BlobDimensionFilter> ParseLeaf(FilterCriteria criterion)
        {
            if (criterion.Field.StartsWith("Dimensions.", StringComparison.Ordinal))
            {
                var operation = StorageFilterQuery.DimensionOperator<BlobDimensionOperator>(criterion);
                var operand = StorageFilterQuery.IsNull(criterion.Value) ? null : criterion.Value is JsonElement json
                    ? BlobDimensionsJsonConverter.ReadFilterOperand(json) : criterion.Value;
                var predicate = BlobDimensionPredicate.Create(criterion.Field["Dimensions.".Length..], operation, operand);
                if (predicate.IsFailure) throw new ArgumentException("Invalid Dimension name, operator, or operand.");
                var dimension = BlobDimensionFilter.Where(predicate.Value);
                var validation = BlobDimensionFilterValidator.Validate(dimension, capabilities.Dimensions);
                if (validation.IsFailure) throw new ArgumentException("Dimension comparison is unsupported by this provider.");
                return new(null, default, null, dimension, dimension.HasPositiveAnchor);
            }

            if (criterion.Field is not ("Container" or "Name"))
                throw new ArgumentException("Unknown storage filter field.");

            var key = StorageFilterQuery.ReadKey(criterion, criterion.Field == "Name" ? 2048 : 256, criterion.Field == "Name");
            var anchored = criterion.Field == "Name" && criterion.Operator is FilterOperator.Equal or FilterOperator.StartsWith;
            return new(criterion.Field, criterion.Operator, key, null, anchored);
        }
    }

    /// <summary>Checks that a seek tuple contains a stored blob name and its SHA-256 lookup hash.</summary>
    /// <param name="position">The name followed by the hash in seek order.</param>
    /// <returns>Whether the tuple has the expected shape.</returns>
    /// <remarks>A persisted name can exceed the current filter input limit on providers such as SQLite.</remarks>
    public static bool IsValidPosition(string[] position) =>
        position is { Length: 2 } && !string.IsNullOrWhiteSpace(position[0]) && StorageFilterQuery.IsHash(position[1]);
}
