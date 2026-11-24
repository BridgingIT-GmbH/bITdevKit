// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Globalization;

/// <summary>Stores a validated canonical scalar without arbitrary persisted CLR objects or numeric coercion.</summary>
/// <example><code>var scalar = CacheDimensionValue.From(42L);</code></example>
public sealed record CacheDimensionValue : IComparable<CacheDimensionValue>
{
    /// <summary>Creates or reads a canonical scalar. Null still requires schema permission at admission.</summary>
    public CacheDimensionValue(CacheDimensionKind kind, string canonicalValue)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        this.Kind = kind;
        this.CanonicalValue = canonicalValue is null ? null : kind switch
        {
            CacheDimensionKind.String => CacheIdentityNormalizer.DimensionString(canonicalValue),
            CacheDimensionKind.Boolean => bool.Parse(canonicalValue) ? "true" : "false",
            CacheDimensionKind.Integer => long.Parse(canonicalValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            CacheDimensionKind.Identifier => Guid.Parse(canonicalValue).ToString("D"),
            CacheDimensionKind.Timestamp => new DateTimeOffset(long.Parse(canonicalValue, CultureInfo.InvariantCulture), TimeSpan.Zero).Ticks.ToString(CultureInfo.InvariantCulture),
            _ => throw new ArgumentException("The scalar is outside its declared bounds.", nameof(canonicalValue))
        };
    }

    /// <summary>Gets the fixed scalar kind.</summary>
    public CacheDimensionKind Kind { get; }

    /// <summary>Gets the portable scalar representation, or null for explicit null.</summary>
    public string CanonicalValue { get; }

    /// <summary>Gets whether this is an explicit null rather than an omitted dimension.</summary>
    public bool IsNull => this.CanonicalValue is null;

    /// <summary>Converts only an accepted CLR type; unsupported numeric aliases never coerce silently.</summary>
    public static CacheDimensionValue From(object value, CacheDimensionKind? nullKind = null) => value switch
    {
        null when nullKind.HasValue => new(nullKind.Value, null),
        string text => new(CacheDimensionKind.String, text),
        bool flag => new(CacheDimensionKind.Boolean, flag ? "true" : "false"),
        sbyte number => Integer(number),
        short number => Integer(number),
        int number => Integer(number),
        long number => Integer(number),
        Guid identifier => new(CacheDimensionKind.Identifier, identifier.ToString("D")),
        DateTimeOffset timestamp => new(CacheDimensionKind.Timestamp, timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture)),
        _ => throw new ArgumentException("Use String, Boolean, signed Integer, Guid, UTC DateTimeOffset, or a schema-typed null.", nameof(value))
    };

    /// <summary>Compares values of one kind; numeric and timestamp order never uses lexical string order.</summary>
    public int CompareTo(CacheDimensionValue other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (this.Kind != other.Kind)
        {
            throw new ArgumentException("Cross-kind scalar comparisons are unsupported.", nameof(other));
        }

        if (this.IsNull || other.IsNull)
        {
            return this.IsNull == other.IsNull ? 0 : this.IsNull ? -1 : 1;
        }

        return this.Kind is CacheDimensionKind.Integer or CacheDimensionKind.Timestamp
            ? long.Parse(this.CanonicalValue, CultureInfo.InvariantCulture).CompareTo(long.Parse(other.CanonicalValue, CultureInfo.InvariantCulture))
            : StringComparer.Ordinal.Compare(this.CanonicalValue, other.CanonicalValue);
    }

    private static CacheDimensionValue Integer(long value) => new(CacheDimensionKind.Integer, value.ToString(CultureInfo.InvariantCulture));
}