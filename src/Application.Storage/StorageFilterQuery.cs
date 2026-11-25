// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>Describes a validated key or Dimension criterion, or a group of criteria, for a storage provider.</summary>
/// <example><code>var field = query.Criteria?.Field;</code></example>
public sealed class StorageFilterCriterion<TDimension>
{
    /// <summary>Creates a key or Dimension criterion with its query constraint.</summary>
    public StorageFilterCriterion(string field, FilterOperator operation, string value, TDimension dimension, bool anchored)
    {
        this.Field = field;
        this.Operator = operation;
        this.Value = value;
        this.Dimension = dimension;
        this.IsAnchored = anchored;
    }

    /// <summary>Creates a group and determines whether its branches have a query constraint.</summary>
    public StorageFilterCriterion(bool isAny, IReadOnlyList<StorageFilterCriterion<TDimension>> children)
    {
        this.IsAny = isAny;
        this.Children = Array.AsReadOnly(children.ToArray());
        this.IsAnchored = isAny ? children.All(child => child.IsAnchored) : children.Any(child => child.IsAnchored);
    }

    /// <summary>Gets the validated logical key field, if this is a key criterion.</summary>
    public string Field { get; }

    /// <summary>Gets the validated key comparison.</summary>
    public FilterOperator Operator { get; }

    /// <summary>Gets the key comparison operand.</summary>
    public string Value { get; }

    /// <summary>Gets the typed Dimension predicate, if this is a Dimension criterion.</summary>
    public TDimension Dimension { get; }

    /// <summary>Gets whether the group matches any child.</summary>
    public bool IsAny { get; }

    /// <summary>Gets the group children, or null for a leaf.</summary>
    public IReadOnlyList<StorageFilterCriterion<TDimension>> Children { get; }

    /// <summary>Gets whether every logical branch has a positive query constraint.</summary>
    public bool IsAnchored { get; }
}

/// <summary>Describes a validated component of a storage seek order.</summary>
/// <example><code>var firstField = query.Order[0].Field;</code></example>
public sealed record StorageFilterOrder(string Field, bool Descending);

/// <summary>Normalizes shared FilterModel criteria and continuation state for storage listing.</summary>
public static class StorageFilterQuery
{
    /// <summary>Validates common page options and resolves the effective page size.</summary>
    public static int PageSize(FilterModel model, int defaultTake, int maxTake)
    {
        var limit = Math.Min(maxTake, 1000);
        if (model is null || model.Page is < 0 or > 1 || model.PageSize < 0 || model.PageSize > limit ||
            !model.NoTracking || model.Includes?.Count > 0 || !string.IsNullOrEmpty(model.Hierarchy))
        {
            throw new ArgumentException("Invalid storage filter or page options.");
        }

        return model.PageSize == 0 ? Math.Min(defaultTake, limit) : model.PageSize;
    }

    /// <summary>Builds the complete seek ordering from supported key fields and tie-breakers.</summary>
    public static IReadOnlyList<StorageFilterOrder> Order(FilterModel model, string[] fields, params string[] tieBreakers)
    {
        var order = new List<StorageFilterOrder>();
        foreach (var term in model.Orderings ?? [])
        {
            var direction = term?.Direction ?? OrderDirection.Ascending;
            if (term is null || !fields.Contains(term.Field, StringComparer.Ordinal) || !Enum.IsDefined(direction) ||
                order.Exists(existing => existing.Field == term.Field))
            {
                throw new ArgumentException("Unsupported or duplicate storage ordering field.");
            }

            order.Add(new(term.Field, direction == OrderDirection.Descending));
        }

        order.AddRange(fields.Where(field => !order.Exists(term => term.Field == field)).Select(field => new StorageFilterOrder(field, false)).ToArray());
        order.AddRange(tieBreakers.Select(field => new StorageFilterOrder(field, false)));
        return order.AsReadOnly();
    }

    /// <summary>Validates the criteria tree and combines leaves using the operational FilterModel grouping rules.</summary>
    public static StorageFilterCriterion<TDimension> Parse<TDimension>(FilterModel model,
        Func<FilterCriteria, StorageFilterCriterion<TDimension>> parseLeaf)
    {
        var leaves = 0;
        var groups = 0;
        return ParseGroup(model.Filters, 0);

        StorageFilterCriterion<TDimension> ParseGroup(List<FilterCriteria> filters, int depth)
        {
            var terms = new List<StorageFilterCriterion<TDimension>>();
            var run = new List<StorageFilterCriterion<TDimension>>();
            foreach (var filter in filters ?? [])
            {
                if (filter is null || !Enum.IsDefined(filter.Logic) || !Enum.IsDefined(filter.Operator) || filter.CustomType != FilterCustomType.None ||
                    filter.CustomParameters?.Count > 0 || !string.IsNullOrEmpty(filter.SpecificationName) ||
                    filter.SpecificationArguments?.Length > 0 || filter.CompositeSpecification is not null)
                {
                    throw new ArgumentException("Unsupported storage filter criterion.");
                }

                if (string.IsNullOrEmpty(filter.Field) && IsNull(filter.Value) && filter.Filters?.Count > 0)
                {
                    if (++groups > 16 || depth == 4) throw new ArgumentException("Storage filter group limit exceeded.");
                    run.Add(ParseGroup(filter.Filters, depth + 1));
                }
                else
                {
                    if (++leaves > 16 || string.IsNullOrEmpty(filter.Field) || filter.Filters?.Count > 0)
                        throw new ArgumentException("Invalid storage criterion or predicate limit exceeded.");
                    run.Add(parseLeaf(filter));
                }

                if (filter.Logic == FilterLogicOperator.And)
                {
                    terms.Add(Collapse(true, run));
                    run = [];
                }
            }

            if (run.Count > 0) terms.Add(Collapse(true, run));
            return terms.Count == 0 ? null : Collapse(false, terms);
        }

        static StorageFilterCriterion<TDimension> Collapse(bool any, List<StorageFilterCriterion<TDimension>> nodes) =>
            nodes.Count == 1 ? nodes[0] : new(any, nodes);
    }

    /// <summary>Validates a key comparison and reads its string operand.</summary>
    public static string ReadKey(FilterCriteria filter, int maximumLength, bool supportsPrefix)
    {
        if (filter.Operator is not (FilterOperator.Equal or FilterOperator.NotEqual) &&
            !(supportsPrefix && filter.Operator == FilterOperator.StartsWith))
        {
            throw new ArgumentException("Unsupported storage key comparison.");
        }

        var key = filter.Value is JsonElement { ValueKind: JsonValueKind.String } json ? json.GetString() : filter.Value as string;
        return IsKey(key, maximumLength) ? key : throw new ArgumentException("Invalid storage key operand.");
    }

    /// <summary>Checks that a storage key is nonempty and within the provider's length limit.</summary>
    public static bool IsKey(string value, int maximumLength) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;

    /// <summary>Checks that a seek component is a hexadecimal SHA-256 key hash.</summary>
    public static bool IsHash(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);

    /// <summary>Maps a supported comparison to the feature's Dimension operator.</summary>
    public static TOperator DimensionOperator<TOperator>(FilterCriteria filter) where TOperator : struct, Enum
    {
        if (filter.Operator is FilterOperator.IsNull or FilterOperator.IsNotNull)
        {
            if (!IsNull(filter.Value)) throw new ArgumentException("A Dimension null check has no operand.");
        }
        else if (filter.Operator is not (FilterOperator.Equal or FilterOperator.NotEqual or FilterOperator.GreaterThan or
            FilterOperator.GreaterThanOrEqual or FilterOperator.LessThan or FilterOperator.LessThanOrEqual) || IsNull(filter.Value))
        {
            throw new ArgumentException("Unsupported Dimension comparison or missing typed operand.");
        }

        return Enum.Parse<TOperator>(filter.Operator.ToString());
    }

    /// <summary>Recognizes both CLR and JSON null operands.</summary>
    public static bool IsNull(object value) => value is null or JsonElement { ValueKind: JsonValueKind.Null };

    /// <summary>Computes the query identity from normalized scope, page size, order, and typed criteria.</summary>
    public static string Hash<TDimension>(object scope, int pageSize, IReadOnlyList<StorageFilterOrder> order,
        StorageFilterCriterion<TDimension> criteria, Func<TDimension, string> canonical) where TDimension : class =>
        HashHelper.ComputeSha256(JsonSerializer.Serialize(new object[] { scope, pageSize, order, Describe(criteria, canonical) }));

    /// <summary>Verifies token protection and request identity before returning the seek position.</summary>
    public static string[] ReadPosition(string token, string purpose, string provider, string client, string type,
        string hash, IContinuationTokenProtector protector)
    {
        var decoded = OpaqueContinuationTokenCodec.Deserialize<FilterToken>(token, purpose, protector);
        if (decoded is null || decoded.Version != 1 || decoded.Operation != purpose || decoded.Provider != provider ||
            decoded.Client != client || decoded.Type != type || decoded.Hash != hash || decoded.Position is null)
        {
            throw new FormatException("Continuation token does not match this storage filter query.");
        }

        return decoded.Position;
    }

    /// <summary>Wraps a provider seek position in a public token bound to the current filter query.</summary>
    /// <param name="position">The provider's JSON string array in seek order, or null on the final page.</param>
    /// <param name="purpose">The continuation token purpose.</param>
    /// <param name="provider">The provider identity bound to the token.</param>
    /// <param name="client">The client identity bound to the token.</param>
    /// <param name="type">The document type, or null for a blob query.</param>
    /// <param name="hash">The normalized query hash.</param>
    /// <param name="protector">The optional continuation token protector.</param>
    /// <param name="isValidPosition">Validates the decoded seek tuple for the storage feature.</param>
    /// <returns>The public token, null on the final page, or a failure for a malformed provider position.</returns>
    public static Result<string> WrapPosition(string position, string purpose, string provider, string client, string type,
        string hash, IContinuationTokenProtector protector, Func<string[], bool> isValidPosition)
    {
        if (position is null) return Result<string>.Success(null);

        string[] values;
        try
        {
            values = JsonSerializer.Deserialize<string[]>(position);
        }
        catch (JsonException)
        {
            values = null;
        }

        if (!isValidPosition(values))
        {
            return Result<string>.Failure(new ValidationError("The provider returned an invalid filter continuation position."));
        }

        return Result<string>.Success(OpaqueContinuationTokenCodec.Serialize(
            new FilterToken(1, purpose, provider, client, type, hash, values), purpose, protector));
    }

    private static object Describe<TDimension>(StorageFilterCriterion<TDimension> node, Func<TDimension, string> canonical)
        where TDimension : class => node is null ? null : node.Children is not null
            ? new object[] { node.IsAny, node.Children.Select(child => Describe(child, canonical)).ToArray() }
            : node.Dimension is not null ? canonical(node.Dimension) : new object[] { node.Field, node.Operator, node.Value };

    private sealed record FilterToken(int Version, string Operation, string Provider, string Client, string Type, string Hash, string[] Position);
}
