// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Describes a portable, bounded selection. An empty selection matches nothing.</summary>
/// <example><code>var selection = CacheSelection.Dimension("region").EqualTo("west");</code></example>
public sealed record CacheSelection
{
    /// <summary>Creates an immutable selection node. Validate it against the collection schema before use.</summary>
    public CacheSelection(CacheSelectionKind kind, string name = null, string text = null,
        IReadOnlyList<CacheDimensionValue> values = null, IReadOnlyList<CacheSelection> children = null)
    {
        this.Kind = kind;
        this.Name = name is null ? null : CacheIdentityNormalizer.Name(name);
        this.Text = text is not null && kind is CacheSelectionKind.Key or CacheSelectionKind.Prefix or CacheSelectionKind.Pattern
            ? CacheIdentityNormalizer.Key(text) : text;
        this.Values = CacheBoundedSnapshot.Copy(values, 256, nameof(values));
        this.Children = CacheBoundedSnapshot.Copy(children, 32, nameof(children));
        this.IsEmpty = kind switch
        {
            CacheSelectionKind.None => true,
            CacheSelectionKind.And => this.Children.Count == 0 || this.Children.Any(c => c.IsEmpty),
            CacheSelectionKind.Or => this.Children.Count == 0 || this.Children.All(c => c.IsEmpty),
            CacheSelectionKind.ContainsAny => this.Values.Count == 0,
            _ => false
        };
    }

    /// <summary>Gets the operation represented by this node.</summary>
    public CacheSelectionKind Kind { get; }

    /// <summary>Gets the canonical dimension name, where applicable.</summary>
    public string Name { get; }

    /// <summary>Gets the key, literal prefix, or escaped pattern.</summary>
    public string Text { get; }

    /// <summary>Gets scalar operands or half-open range endpoints.</summary>
    public IReadOnlyList<CacheDimensionValue> Values { get; }

    /// <summary>Gets the immutable operands of an explicit conjunction/disjunction.</summary>
    public IReadOnlyList<CacheSelection> Children { get; }

    /// <summary>Gets whether the bounded grammar provably matches nothing, without consulting metadata.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty { get; }

    /// <summary>Gets a selection that matches nothing.</summary>
    public static CacheSelection None { get; } = new(CacheSelectionKind.None);

    /// <summary>Selects exactly one opaque key. Wildcard characters remain literal.</summary>
    public static CacheSelection Key(string key) => new(CacheSelectionKind.Key, text: CacheIdentityNormalizer.Key(key));

    /// <summary>Selects keys starting with a literal, nonempty prefix.</summary>
    public static CacheSelection Prefix(string prefix) => new(CacheSelectionKind.Prefix, text: CacheIdentityNormalizer.Key(prefix));

    /// <summary>Selects whole keys using * and ?, with backslash escaping only *, ?, and backslash.</summary>
    public static CacheSelection Pattern(string pattern) => new(CacheSelectionKind.Pattern, text: CacheIdentityNormalizer.Key(pattern));

    /// <summary>Begins a named dimension predicate.</summary>
    public static CacheDimensionSelection Dimension(string name) => new(name);

    /// <summary>Selects publication instants in a half-open UTC range.</summary>
    public static CacheSelection PublishedBetween(DateTimeOffset lower, DateTimeOffset upper) =>
        new(CacheSelectionKind.PublishedRange, values: [CacheDimensionValue.From(lower), CacheDimensionValue.From(upper)]);

    /// <summary>Selects exact stored-byte sizes in a half-open nonnegative range.</summary>
    public static CacheSelection StoredSizeBetween(long lower, long upper) =>
        new(CacheSelectionKind.StoredSizeRange, values: [CacheDimensionValue.From(lower), CacheDimensionValue.From(upper)]);

    /// <summary>Requires all operands. An empty conjunction matches nothing.</summary>
    public static CacheSelection And(params CacheSelection[] children) => new(CacheSelectionKind.And, children: children);

    /// <summary>Requires at least one operand. An empty disjunction matches nothing.</summary>
    public static CacheSelection Or(params CacheSelection[] children) => new(CacheSelectionKind.Or, children: children);
}

/// <summary>Builds a typed predicate for one declared dimension.</summary>
/// <example><code>var selection = CacheSelection.Dimension("stock").Overlaps(10L, 20L);</code></example>
public sealed class CacheDimensionSelection(string name)
{
    private readonly string name = CacheIdentityNormalizer.Name(name);

    /// <summary>Selects scalar equality, including a schema-typed explicit null.</summary>
    public CacheSelection EqualTo(object value, CacheDimensionKind? nullKind = null) =>
        new(CacheSelectionKind.Equal, this.name, values: [CacheDimensionValue.From(value, nullKind)]);

    /// <summary>Selects a set containing the supplied scalar.</summary>
    public CacheSelection Contains(object value, CacheDimensionKind? nullKind = null) =>
        new(CacheSelectionKind.Contains, this.name, values: [CacheDimensionValue.From(value, nullKind)]);

    /// <summary>Selects sets containing any member of a bounded, same-kind set.</summary>
    public CacheSelection ContainsAny(CacheDimensionKind kind, params object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length > 256)
        {
            throw new ArgumentException("A membership predicate exceeds its fixed bound.", nameof(values));
        }

        return new(CacheSelectionKind.ContainsAny, this.name, values: values.Select(v => CacheDimensionValue.From(v, kind)).Distinct().ToArray());
    }

    /// <summary>Selects half-open intervals overlapping the supplied Integer or Timestamp range.</summary>
    public CacheSelection Overlaps(object lower, object upper) =>
        new(CacheSelectionKind.Overlap, this.name, values: [CacheDimensionValue.From(lower), CacheDimensionValue.From(upper)]);

    /// <summary>Selects entries where the dimension is present, including null and an empty set.</summary>
    public CacheSelection IsPresent() => new(CacheSelectionKind.Present, this.name);

    /// <summary>Selects entries where the dimension was omitted.</summary>
    public CacheSelection IsAbsent() => new(CacheSelectionKind.Absent, this.name);
}

/// <summary>Identifies the finite supported selection grammar.</summary>
public enum CacheSelectionKind
{
    /// <summary>Matches nothing.</summary>
    None,

    /// <summary>Opaque exact key.</summary>
    Key,

    /// <summary>Literal key prefix.</summary>
    Prefix,

    /// <summary>Escaped whole-key glob.</summary>
    Pattern,

    /// <summary>Scalar dimension equality.</summary>
    Equal,

    /// <summary>One set member.</summary>
    Contains,

    /// <summary>Any supplied set member.</summary>
    ContainsAny,

    /// <summary>Half-open interval overlap.</summary>
    Overlap,

    /// <summary>Dimension presence.</summary>
    Present,

    /// <summary>Dimension omission.</summary>
    Absent,

    /// <summary>Half-open publication UTC range.</summary>
    PublishedRange,

    /// <summary>Half-open stored-byte range.</summary>
    StoredSizeRange,

    /// <summary>Explicit conjunction.</summary>
    And,

    /// <summary>Explicit disjunction.</summary>
    Or
}