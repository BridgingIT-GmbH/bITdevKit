// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Describes the storage and indexed query profile for document Dimensions.</summary>
/// <example><code>var supported = capabilities.Supports(DocumentDimensionValueType.Decimal, DocumentDimensionOperator.Equal);</code></example>
public sealed record DocumentDimensionCapabilities
{
    /// <summary>Gets the unsupported default profile.</summary>
    public static DocumentDimensionCapabilities Unsupported { get; } = new();

    /// <summary>Gets whether collections can be stored and round-tripped.</summary>
    public bool SupportsDimensions { get; init; }

    /// <summary>Gets whether dimension predicates execute in the backend.</summary>
    public bool SupportsQueries { get; init; }

    /// <summary>Gets whether decimal comparisons beyond equality use the index.</summary>
    public bool SupportsDecimalComparisons { get; init; }

    /// <summary>Gets the maximum entries per owner.</summary>
    public int MaximumCount => DocumentDimensionLimits.MaximumCount;

    /// <summary>Gets the maximum name characters.</summary>
    public int MaximumNameLength => DocumentDimensionLimits.MaximumNameLength;

    /// <summary>Gets the maximum UTF-8 value bytes.</summary>
    public int MaximumStringBytes => DocumentDimensionLimits.MaximumStringBytes;

    /// <summary>Gets the maximum predicate count.</summary>
    public int MaximumPredicates => DocumentDimensionLimits.MaximumPredicates;

    /// <summary>Gets the maximum group count.</summary>
    public int MaximumGroups => DocumentDimensionLimits.MaximumGroups;

    /// <summary>Gets whether supported value predicates are eligible for typed indexes.</summary>
    public bool UsesIndexedValuePredicates => this.SupportsQueries;

    /// <summary>Gets the maximum group nesting.</summary>
    public int MaximumGroupDepth => DocumentDimensionLimits.MaximumGroupDepth;

    /// <summary>Gets the exact string equality contract.</summary>
    public string StringComparison => "Ordinal";

    /// <summary>Gets the per-kind operator matrix as names for transport and operational inspection.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> SupportedOperators => Enum.GetValues<DocumentDimensionValueType>()
        .ToDictionary(kind => kind.ToString(), kind => (IReadOnlyList<string>)Array.AsReadOnly(Enum.GetValues<DocumentDimensionOperator>()
            .Where(operation => this.Supports(kind, operation)).Select(operation => operation.ToString()).ToArray()));

    /// <summary>Tests whether an operator and logical kind are supported.</summary>
    /// <example><code>var canCompare = capabilities.Supports(DocumentDimensionValueType.Decimal, DocumentDimensionOperator.GreaterThan);</code></example>
    public bool Supports(DocumentDimensionValueType kind, DocumentDimensionOperator operation) => this.SupportsQueries
        && Enum.IsDefined(operation) && Enum.IsDefined(kind)
        && (operation >= DocumentDimensionOperator.Exists
            || kind != DocumentDimensionValueType.Null
                && (kind != DocumentDimensionValueType.Decimal || operation == DocumentDimensionOperator.Equal || this.SupportsDecimalComparisons)
                && (operation is DocumentDimensionOperator.Equal or DocumentDimensionOperator.NotEqual
                    || kind is DocumentDimensionValueType.Integer or DocumentDimensionValueType.Decimal or DocumentDimensionValueType.DateTime));
}
