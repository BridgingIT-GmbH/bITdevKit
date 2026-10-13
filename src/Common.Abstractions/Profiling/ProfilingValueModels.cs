// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Identifies the exact scalar type retained in profiling metadata.</summary>
/// <example><code>var value = ProfilingValueType.String;</code></example>
public enum ProfilingValueType
{
    /// <summary>String classification.</summary>
    /// <example><code>var value = ProfilingValueType.String;</code></example>
    String,
    /// <summary>Boolean classification.</summary>
    /// <example><code>var value = ProfilingValueType.Boolean;</code></example>
    Boolean,
    /// <summary>Int64 classification.</summary>
    /// <example><code>var value = ProfilingValueType.Int64;</code></example>
    Int64,
    /// <summary>Decimal classification.</summary>
    /// <example><code>var value = ProfilingValueType.Decimal;</code></example>
    Decimal,
    /// <summary>Double classification.</summary>
    /// <example><code>var value = ProfilingValueType.Double;</code></example>
    Double,
    /// <summary>UtcDateTime classification.</summary>
    /// <example><code>var value = ProfilingValueType.UtcDateTime;</code></example>
    UtcDateTime,
}

/// <summary>Stores a typed scalar as invariant lossless text, avoiding browser number precision loss.</summary>
/// <example><code>ProfilingValue.TryCreate(9007199254740993L, out var value);</code></example>
public sealed record ProfilingValue
{
    /// <summary>Creates and validates a scalar received from storage or JSON.</summary>
    /// <example><code>var value = new ProfilingValue(ProfilingValueType.Int64, "42");</code></example>
    [JsonConstructor]
    public ProfilingValue(ProfilingValueType type, string scalar)
    {
        ArgumentNullException.ThrowIfNull(scalar);
        this.Type = type;
        this.Scalar = type switch
        {
            ProfilingValueType.String => scalar,
            ProfilingValueType.Boolean when bool.TryParse(scalar, out var b) => b ? "true" : "false",
            ProfilingValueType.Int64 when long.TryParse(scalar, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i.ToString(CultureInfo.InvariantCulture),
            ProfilingValueType.Decimal when decimal.TryParse(scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d.ToString("G29", CultureInfo.InvariantCulture),
            ProfilingValueType.Double when double.TryParse(scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && double.IsFinite(f) => (f == 0d ? 0d : f).ToString("R", CultureInfo.InvariantCulture),
            ProfilingValueType.UtcDateTime when DateTimeOffset.TryParse(scalar, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc) && utc.Offset == TimeSpan.Zero => utc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            _ => throw new ArgumentException("A supported finite scalar or UTC instant is required.", nameof(scalar))
        };
    }

    /// <summary>Gets the exact type tag.</summary>
    /// <example><code>var type = value.Type;</code></example>
    [JsonConverter(typeof(JsonStringEnumConverter<ProfilingValueType>))]
    public ProfilingValueType Type { get; }

    /// <summary>Gets invariant canonical scalar text.</summary>
    /// <example><code>var text = value.Scalar;</code></example>
    public string Scalar { get; }

    /// <summary>Converts only supported scalar values without retaining arbitrary application objects.</summary>
    /// <example><code>var accepted = ProfilingValue.TryCreate(true, out var value);</code></example>
    public static bool TryCreate(object source, out ProfilingValue value)
    {
        value = null;
        switch (source)
        {
            case string text: value = new(ProfilingValueType.String, text); break;
            case bool boolean: value = new(ProfilingValueType.Boolean, boolean ? "true" : "false"); break;
            case sbyte or byte or short or ushort or int or uint or long:
                value = new(ProfilingValueType.Int64, Convert.ToInt64(source, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)); break;
            case ulong integer when integer <= long.MaxValue: value = new(ProfilingValueType.Int64, integer.ToString(CultureInfo.InvariantCulture)); break;
            case decimal number: value = new(ProfilingValueType.Decimal, number.ToString("G29", CultureInfo.InvariantCulture)); break;
            case float number when float.IsFinite(number): value = new(ProfilingValueType.Double, ((double)number).ToString("R", CultureInfo.InvariantCulture)); break;
            case double number when double.IsFinite(number): value = new(ProfilingValueType.Double, number.ToString("R", CultureInfo.InvariantCulture)); break;
            case DateTimeOffset utc when utc.Offset == TimeSpan.Zero: value = new(ProfilingValueType.UtcDateTime, utc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)); break;
            case DateTime utc when utc.Kind == DateTimeKind.Utc: value = new(ProfilingValueType.UtcDateTime, utc.ToString("O", CultureInfo.InvariantCulture)); break;
        }

        return value is not null;
    }
}

/// <summary>Serializes UTC profiling timestamps with a literal Z suffix.</summary>
/// <example><code>var options = new JsonSerializerOptions(); options.Converters.Add(new ProfilingUtcJsonConverter());</code></example>
public sealed class ProfilingUtcJsonConverter : JsonConverter<DateTimeOffset>
{
    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTimeOffset();
        if (value.Offset != TimeSpan.Zero) { throw new JsonException("A UTC profiling timestamp is required."); }

        return value;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        if (value.Offset != TimeSpan.Zero) { throw new JsonException("A UTC profiling timestamp is required."); }

        writer.WriteStringValue(value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
    }
}

/// <summary>Compares profiling names using invariant uppercasing and ordinal equality.</summary>
/// <example><code>var identity = ProfilingKeyComparer.Canonicalize("Load");</code></example>
public sealed class ProfilingKeyComparer : IEqualityComparer<string>
{
    /// <summary>Gets the shared comparer.</summary>
    /// <example><code>var names = new Dictionary&lt;string, int&gt;(ProfilingKeyComparer.Instance);</code></example>
    public static ProfilingKeyComparer Instance { get; } = new();
    /// <summary>Produces the portable comparison key without trimming or culture-sensitive folding.</summary>
    /// <example><code>var key = ProfilingKeyComparer.Canonicalize("load");</code></example>
    public static string Canonicalize(string value) => value?.ToUpperInvariant();
    /// <inheritdoc />
    public bool Equals(string x, string y) => StringComparer.Ordinal.Equals(Canonicalize(x), Canonicalize(y));
    /// <inheritdoc />
    public int GetHashCode(string obj) => StringComparer.Ordinal.GetHashCode(Canonicalize(obj));
}

/// <summary>Identifies segment ancestry by component boundaries, with a defensive copy of labels.</summary>
/// <example><code>var path = new ProfilingSegmentPath(new[] { "Load", "Read" });</code></example>
public sealed class ProfilingSegmentPath : IEquatable<ProfilingSegmentPath>
{
    /// <summary>Creates a structured path whose identity includes every component boundary.</summary>
    /// <example><code>var path = new ProfilingSegmentPath(new[] { "Load" });</code></example>
    [JsonConstructor]
    public ProfilingSegmentPath(IReadOnlyList<string> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        if (components.Count == 0 || components.Any(string.IsNullOrWhiteSpace))
        { throw new ArgumentException("A path requires nonempty components.", nameof(components)); }

        this.Components = Array.AsReadOnly(components.ToArray());
        this.ComparisonKey = string.Concat(components.Select(component =>
        {
            var key = ProfilingKeyComparer.Canonicalize(component);
            return key.Length.ToString(CultureInfo.InvariantCulture) + ":" + key;
        }));
    }

    /// <summary>Gets immutable display components.</summary>
    /// <example><code>var key = path.Components[0];</code></example>
    public IReadOnlyList<string> Components { get; }
    /// <summary>Gets the unambiguous ordinal comparison representation.</summary>
    /// <example><code>var identity = path.ComparisonKey;</code></example>
    [JsonIgnore]
    public string ComparisonKey { get; }
    /// <inheritdoc />
    public bool Equals(ProfilingSegmentPath other) => other is not null && this.ComparisonKey == other.ComparisonKey;
    /// <inheritdoc />
    public override bool Equals(object obj) => obj is ProfilingSegmentPath path && this.Equals(path);
    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.ComparisonKey);
}
/// <summary>Declares the reduction of completed invocation samples.</summary>
/// <example><code>var value = MeasurementAggregation.Sum;</code></example>
public enum MeasurementAggregation
{
    /// <summary>Sum classification.</summary>
    /// <example><code>var value = MeasurementAggregation.Sum;</code></example>
    Sum,
    /// <summary>Min classification.</summary>
    /// <example><code>var value = MeasurementAggregation.Min;</code></example>
    Min,
    /// <summary>Max classification.</summary>
    /// <example><code>var value = MeasurementAggregation.Max;</code></example>
    Max,
    /// <summary>Average classification.</summary>
    /// <example><code>var value = MeasurementAggregation.Average;</code></example>
    Average,
    /// <summary>Last classification.</summary>
    /// <example><code>var value = MeasurementAggregation.Last;</code></example>
    Last,
}

/// <summary>One named typed comparison value.</summary>
/// <example><code>var value = new ProfilingDimension();</code></example>
public sealed record ProfilingDimension
{
    /// <summary>Gets the original dimension name.</summary>
    /// <example><code>var value = record.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Gets the typed scalar.</summary>
    /// <example><code>var value = record.Value;</code></example>
    public ProfilingValue Value { get; init; }

}

/// <summary>One finite named measurement and its unit.</summary>
/// <example><code>var value = new ProfilingMeasurement();</code></example>
public sealed record ProfilingMeasurement
{
    /// <summary>Gets the measurement name.</summary>
    /// <example><code>var value = record.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Gets the finite numeric scalar.</summary>
    /// <example><code>var value = record.Value;</code></example>
    public ProfilingValue Value { get; init; }

    /// <summary>Gets the case-sensitive unit.</summary>
    /// <example><code>var value = record.Unit;</code></example>
    public string Unit { get; init; }

    /// <summary>Gets the invocation reducer.</summary>
    /// <example><code>var value = record.Aggregation;</code></example>
    public MeasurementAggregation Aggregation { get; init; }

}
