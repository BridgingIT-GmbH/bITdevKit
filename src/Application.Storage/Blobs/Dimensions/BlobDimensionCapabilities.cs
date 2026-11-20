// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Describes the storage and indexed query profile for blob Dimensions.</summary>
/// <example><code>var supported = capabilities.Supports(BlobDimensionValueType.Decimal, BlobDimensionOperator.Equal);</code></example>
public sealed record BlobDimensionCapabilities
{
    /// <summary>Gets the unsupported default profile.</summary>
    public static BlobDimensionCapabilities Unsupported { get; } = new();

    /// <summary>Gets whether collections can be stored and round-tripped.</summary>
    public bool SupportsDimensions { get; init; }

    /// <summary>Gets whether dimension predicates execute in the backend.</summary>
    public bool SupportsQueries { get; init; }

    /// <summary>Gets whether decimal comparisons beyond equality use the index.</summary>
    public bool SupportsDecimalComparisons { get; init; }

    /// <summary>Gets the maximum entries per owner.</summary>
    public int MaximumCount => BlobDimensionLimits.MaximumCount;

    /// <summary>Gets the maximum name characters.</summary>
    public int MaximumNameLength => BlobDimensionLimits.MaximumNameLength;

    /// <summary>Gets the maximum UTF-8 value bytes.</summary>
    public int MaximumStringBytes => BlobDimensionLimits.MaximumStringBytes;

    /// <summary>Gets the maximum predicate count.</summary>
    public int MaximumPredicates => BlobDimensionLimits.MaximumPredicates;

    /// <summary>Gets the maximum group count.</summary>
    public int MaximumGroups => BlobDimensionLimits.MaximumGroups;

    /// <summary>Gets whether supported value predicates are eligible for typed indexes.</summary>
    public bool UsesIndexedValuePredicates => this.SupportsQueries;

    /// <summary>Gets the maximum group nesting.</summary>
    public int MaximumGroupDepth => BlobDimensionLimits.MaximumGroupDepth;

    /// <summary>Gets the exact string equality contract.</summary>
    public string StringComparison => "Ordinal";

    /// <summary>Gets the per-kind operator matrix as names for transport and operational inspection.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> SupportedOperators => Enum.GetValues<BlobDimensionValueType>()
        .ToDictionary(kind => kind.ToString(), kind => (IReadOnlyList<string>)Array.AsReadOnly(Enum.GetValues<BlobDimensionOperator>()
            .Where(operation => this.Supports(kind, operation)).Select(operation => operation.ToString()).ToArray()));

    /// <summary>Tests whether an operator and logical kind are supported.</summary>
    /// <example><code>var canCompare = capabilities.Supports(BlobDimensionValueType.Decimal, BlobDimensionOperator.GreaterThan);</code></example>
    public bool Supports(BlobDimensionValueType kind, BlobDimensionOperator operation) => this.SupportsQueries
        && Enum.IsDefined(operation) && Enum.IsDefined(kind)
        && (operation >= BlobDimensionOperator.Exists
            || kind != BlobDimensionValueType.Null
                && (kind != BlobDimensionValueType.Decimal || operation == BlobDimensionOperator.Equal || this.SupportsDecimalComparisons)
                && (operation is BlobDimensionOperator.Equal or BlobDimensionOperator.NotEqual
                    || kind is BlobDimensionValueType.Integer or BlobDimensionValueType.Decimal or BlobDimensionValueType.DateTime));
}
