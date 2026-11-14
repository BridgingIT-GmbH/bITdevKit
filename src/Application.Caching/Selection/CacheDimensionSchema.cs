// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Declares the allowed kind, form, and null policy for one dependency dimension.</summary>
/// <param name="Name">The stable dimension name.</param>
/// <param name="Kind">The only accepted scalar kind.</param>
/// <param name="Form">The accepted shape.</param>
/// <param name="AllowNull">Whether an explicit null scalar/set member is allowed.</param>
/// <example><code>var schema = new CacheDimensionSchema("catalog", CacheDimensionKind.Identifier);</code></example>
public sealed record CacheDimensionSchema(
    string Name,
    CacheDimensionKind Kind,
    CacheDimensionForm Form = CacheDimensionForm.Scalar,
    bool AllowNull = false);

/// <summary>Stores one immutable dependency dimension, including explicit empty sets and typed nulls.</summary>
/// <example><code>var dimension = CacheDimension.Scalar("region", "west");</code></example>
public sealed record CacheDimension
{
    /// <summary>Creates a dimension using an explicitly declared shape and defensive scalar snapshot.</summary>
    public CacheDimension(string name, CacheDimensionKind kind, CacheDimensionForm form, IReadOnlyList<CacheDimensionValue> values)
    {
        this.Name = CacheIdentityNormalizer.Name(name);
        this.Kind = kind;
        this.Form = form;
        var snapshot = CacheBoundedSnapshot.Copy(values ?? throw new ArgumentNullException(nameof(values)), 256, nameof(values));
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(form) || snapshot.Any(v => v.Kind != kind))
        {
            throw new ArgumentException("Dimensions require one declared kind and a bounded set of matching scalars.", nameof(values));
        }

        if (form == CacheDimensionForm.Scalar && snapshot.Count != 1)
        {
            throw new ArgumentException("A scalar dimension has exactly one value.", nameof(values));
        }

        if (form == CacheDimensionForm.Interval &&
            (snapshot.Count != 2 || kind is not (CacheDimensionKind.Integer or CacheDimensionKind.Timestamp) ||
             snapshot.Any(v => v.IsNull) || snapshot[0].CompareTo(snapshot[1]) >= 0))
        {
            throw new ArgumentException("Intervals require non-null ordered Integer or Timestamp endpoints.", nameof(values));
        }

        this.Values = form == CacheDimensionForm.Set ? Array.AsReadOnly(snapshot.Distinct().ToArray()) : snapshot;
    }

    /// <summary>Gets the canonical dimension name.</summary>
    public string Name { get; }

    /// <summary>Gets the declared kind.</summary>
    public CacheDimensionKind Kind { get; }

    /// <summary>Gets the declared shape.</summary>
    public CacheDimensionForm Form { get; }

    /// <summary>Gets immutable scalar values, set members, or lower/upper interval endpoints.</summary>
    public IReadOnlyList<CacheDimensionValue> Values { get; }

    /// <summary>Creates a scalar or schema-typed explicit null.</summary>
    public static CacheDimension Scalar(string name, object value, CacheDimensionKind? nullKind = null)
    {
        var scalar = CacheDimensionValue.From(value, nullKind);
        return new(name, scalar.Kind, CacheDimensionForm.Scalar, [scalar]);
    }

    /// <summary>Creates a typed set. Empty sets retain presence and a declared kind.</summary>
    public static CacheDimension Set(string name, CacheDimensionKind kind, params object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length > 256)
        {
            throw new ArgumentException("A dimension set exceeds its membership bound.", nameof(values));
        }

        return new(name, kind, CacheDimensionForm.Set, values.Select(v => CacheDimensionValue.From(v, kind)).ToArray());
    }

    /// <summary>Creates a half-open interval with exact Integer or UTC Timestamp endpoints.</summary>
    public static CacheDimension Interval(string name, object lower, object upper)
    {
        var start = CacheDimensionValue.From(lower);
        return new(name, start.Kind, CacheDimensionForm.Interval, [start, CacheDimensionValue.From(upper)]);
    }
}