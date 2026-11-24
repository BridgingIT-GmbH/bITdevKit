// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>Sets the mandatory storage boundaries for a document filter query.</summary>
/// <param name="PartitionKey">An optional partition shared by every query branch.</param>
/// <param name="RowKeyPrefix">An optional row prefix within the selected partition.</param>
/// <param name="AllowFullScan">Whether this request approves a full scan when the client permits one.</param>
/// <example><code>var scope = new DocumentFilterScope("invoices");</code></example>
public sealed record DocumentFilterScope(string PartitionKey = null, string RowKeyPrefix = null, bool AllowFullScan = false);

/// <summary>Represents a validated document filter query passed to a storage provider.</summary>
/// <example><code>var criteria = query.Criteria;</code></example>
public sealed class DocumentFilterQuery
{
    private DocumentFilterQuery(DocumentFilterScope scope, StorageFilterCriterion<DocumentDimensionFilter> criteria,
        IReadOnlyList<StorageFilterOrder> order, string[] position)
    {
        this.Scope = scope;
        this.Criteria = criteria;
        this.Order = order;
        this.Position = position is null ? null : Array.AsReadOnly(position);
    }

    /// <summary>Gets the scope that applies to every filter branch.</summary>
    public DocumentFilterScope Scope { get; }

    /// <summary>Gets the validated predicate tree.</summary>
    public StorageFilterCriterion<DocumentDimensionFilter> Criteria { get; }

    /// <summary>Gets the complete seek order, including tie-breakers.</summary>
    public IReadOnlyList<StorageFilterOrder> Order { get; }

    /// <summary>Gets the cursor position in seek order, or null for the first page.</summary>
    public IReadOnlyList<string> Position { get; }

    /// <summary>Identifies these continuation tokens separately from native listing tokens.</summary>
    public const string Purpose = "document-filter-search";

    /// <summary>Validates a request and binds any continuation to its normalized filter, scope, and ordering.</summary>
    public static Result<(DocumentFilterQuery Query, int PageSize, string Hash)> Prepare(
        DocumentFilterScope scope, FilterModel filter, string token, string providerName, string clientName,
        string documentType, DocumentStoreOptions options, DocumentStoreProviderCapabilities capabilities, IContinuationTokenProtector protector)
    {
        if (capabilities?.SupportsFilterModel != true)
            return Result<(DocumentFilterQuery, int, string)>.Failure(new DocumentStoreQueryNotSupportedError("Provider does not support FilterModel queries."));

        options ??= new();
        var optionsResult = options.Validate();
        if (optionsResult.IsFailure) return Result<(DocumentFilterQuery, int, string)>.Failure(optionsResult);

        try
        {
            if (scope is null || scope.PartitionKey is not null && !StorageFilterQuery.IsKey(scope.PartitionKey, 256) ||
                scope.RowKeyPrefix is not null && (scope.PartitionKey is null || !StorageFilterQuery.IsKey(scope.RowKeyPrefix, 256)))
                throw new ArgumentException("Invalid document partition or row-key prefix.");

            var size = StorageFilterQuery.PageSize(filter, options.DefaultTake, options.MaxTake);
            var order = StorageFilterQuery.Order(filter, ["PartitionKey", "RowKey"], "PartitionKeyHash", "RowKeyHash");
            var criteria = StorageFilterQuery.Parse<DocumentDimensionFilter>(filter, ParseLeaf);
            DocumentKey? key = scope.PartitionKey is null ? null : new(scope.PartitionKey, scope.RowKeyPrefix ?? string.Empty);
            var shape = key is null && criteria?.IsAnchored == true
                ? Result.Success()
                : DocumentQueryValidator.ValidateShape(key, DocumentKeyFilter.RowKeyPrefixMatch, scope.AllowFullScan, capabilities, options);
            if (shape.IsFailure) return Result<(DocumentFilterQuery, int, string)>.Failure(shape);

            var hash = StorageFilterQuery.Hash(scope, size, order, criteria, dimension => dimension.ToCanonicalString());
            string[] position = null;
            if (token is not null)
            {
                position = StorageFilterQuery.ReadPosition(token, Purpose, providerName, clientName, documentType, hash, protector);
                if (!IsValidPosition(position))
                    throw new FormatException("Invalid continuation position.");
            }

            return Result<(DocumentFilterQuery, int, string)>.Success((new(scope, criteria, order, position), size, hash));
        }
        catch (FormatException)
        {
            return Result<(DocumentFilterQuery, int, string)>.Failure(new DocumentStoreInvalidContinuationTokenError("Invalid FilterModel continuation token."));
        }
        catch (ArgumentException exception)
        {
            return Result<(DocumentFilterQuery, int, string)>.Failure(new ValidationError(exception.Message));
        }
        catch (JsonException)
        {
            return Result<(DocumentFilterQuery, int, string)>.Failure(new ValidationError("Invalid typed Dimension operand."));
        }

        StorageFilterCriterion<DocumentDimensionFilter> ParseLeaf(FilterCriteria criterion)
        {
            if (criterion.Field.StartsWith("Dimensions.", StringComparison.Ordinal))
            {
                var operation = StorageFilterQuery.DimensionOperator<DocumentDimensionOperator>(criterion);
                var operand = StorageFilterQuery.IsNull(criterion.Value) ? null : criterion.Value is JsonElement json
                    ? DocumentDimensionsJsonConverter.ReadFilterOperand(json) : criterion.Value;
                var predicate = DocumentDimensionPredicate.Create(criterion.Field["Dimensions.".Length..], operation, operand);
                if (predicate.IsFailure) throw new ArgumentException("Invalid Dimension name, operator, or operand.");
                var dimension = DocumentDimensionFilter.Where(predicate.Value);
                var validation = DocumentDimensionFilterValidator.Validate(dimension, capabilities.Dimensions);
                if (validation.IsFailure) throw new ArgumentException("Dimension comparison is unsupported by this provider.");
                return new(null, default, null, dimension, dimension.HasPositiveAnchor);
            }

            if (criterion.Field is not ("PartitionKey" or "RowKey"))
                throw new ArgumentException("Unknown storage filter field.");

            var key = StorageFilterQuery.ReadKey(criterion, 256, true);
            var anchored = criterion.Field == "PartitionKey" && criterion.Operator == FilterOperator.Equal;
            return new(criterion.Field, criterion.Operator, key, null, anchored);
        }
    }

    /// <summary>Checks that a seek tuple contains two document keys and their SHA-256 lookup hashes.</summary>
    /// <param name="position">The key components in the query order, followed by the partition and row key hashes.</param>
    /// <returns>Whether the tuple has the expected shape.</returns>
    public static bool IsValidPosition(string[] position) =>
        position is { Length: 4 } && StorageFilterQuery.IsKey(position[0], 256) && StorageFilterQuery.IsKey(position[1], 256) &&
        StorageFilterQuery.IsHash(position[2]) && StorageFilterQuery.IsHash(position[3]);
}
