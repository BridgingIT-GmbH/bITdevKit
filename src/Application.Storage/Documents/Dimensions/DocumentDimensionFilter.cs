// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>An immutable leaf or bounded AND/OR expression.</summary>
/// <example><code>var filter = DocumentDimensionFilter.Where(DocumentDimensionPredicate.Create("year", DocumentDimensionOperator.Equal, 2026).Value);</code></example>
[JsonConverter(typeof(DocumentDimensionFilterJsonConverter))]
public sealed class DocumentDimensionFilter
{
    private DocumentDimensionFilter(DocumentDimensionPredicate predicate, bool any, DocumentDimensionFilter[] children)
    {
        this.Predicate = predicate;
        this.IsAny = any;
        this.Children = Array.AsReadOnly(children.ToArray());
    }

    /// <summary>Gets the leaf predicate, or null for a group.</summary>
    public DocumentDimensionPredicate Predicate { get; }

    /// <summary>Gets whether the group is OR rather than AND.</summary>
    public bool IsAny { get; }

    /// <summary>Gets the immutable group children.</summary>
    public IReadOnlyList<DocumentDimensionFilter> Children { get; }

    /// <summary>Creates a predicate leaf.</summary>
    /// <example><code>var leaf = DocumentDimensionFilter.Where(predicate.Value);</code></example>
    public static DocumentDimensionFilter Where(DocumentDimensionPredicate predicate) => new(predicate ?? throw new ArgumentNullException(nameof(predicate)), false, []);

    /// <summary>Creates an AND group; validation enforces size and depth.</summary>
    /// <example><code>var both = DocumentDimensionFilter.All(yearFilter, approvalFilter);</code></example>
    public static DocumentDimensionFilter All(params DocumentDimensionFilter[] children) => new(null, false, children ?? throw new ArgumentNullException(nameof(children)));

    /// <summary>Creates an OR group; validation enforces size and depth.</summary>
    /// <example><code>var either = DocumentDimensionFilter.Any(yearFilter, approvalFilter);</code></example>
    public static DocumentDimensionFilter Any(params DocumentDimensionFilter[] children) => new(null, true, children ?? throw new ArgumentNullException(nameof(children)));

    /// <summary>Tests whether every OR branch has a positive indexed anchor.</summary>
    public bool HasPositiveAnchor => this.Predicate is { } predicate
        ? predicate.Operator is not (DocumentDimensionOperator.NotEqual or DocumentDimensionOperator.Missing)
        : this.Children.Count > 0 && (this.IsAny ? this.Children.All(child => child?.HasPositiveAnchor == true) : this.Children.Any(child => child?.HasPositiveAnchor == true));

    /// <summary>Returns canonical logical identity for protected tokens and cache keys, without NameIds.</summary>
    /// <example><code>var logicalIdentity = filter.ToCanonicalString();</code></example>
    public string ToCanonicalString()
    {
        if (this.Predicate is not { } predicate)
            return JsonSerializer.Serialize(new object[] { this.IsAny ? "or" : "and", this.Children.Select(child => child?.ToCanonicalString()).ToArray() });
        var scalar = predicate.Operand;
        var value = scalar?.Value switch
        {
            decimal number => number.ToString("G29", CultureInfo.InvariantCulture),
            DateTime date => date.Ticks.ToString(CultureInfo.InvariantCulture),
            string text => Convert.ToHexString(Encoding.UTF8.GetBytes(text)),
            bool flag => flag ? "1" : "0",
            int integer => integer.ToString(CultureInfo.InvariantCulture),
            _ => null
        };
        return JsonSerializer.Serialize(new object[] { DocumentDimensionValues.CanonicalizeName(predicate.Name), predicate.Operator, scalar?.ValueType, value });
    }
}
