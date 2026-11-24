// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections;
using BridgingIT.DevKit.Common;

/// <summary>An immutable case-insensitive collection of explicitly supplied blob dimensions.</summary>
/// <example><code>var dimensions = BlobDimensions.Create(new Dictionary&lt;string, object&gt; { ["year"] = 2026 });</code></example>
[System.Text.Json.Serialization.JsonConverter(typeof(BlobDimensionsJsonConverter))]
public sealed class BlobDimensions : IReadOnlyDictionary<string, BlobDimensionValue>
{
    private readonly Dictionary<string, BlobDimensionValue> values;

    private BlobDimensions(Dictionary<string, BlobDimensionValue> values) => this.values = values;

    /// <summary>Gets an explicitly empty immutable collection.</summary>
    public static BlobDimensions Empty { get; } = new(new(StringComparer.OrdinalIgnoreCase));

    /// <inheritdoc />
    public int Count => this.values.Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys => this.values.Keys;

    /// <inheritdoc />
    public IEnumerable<BlobDimensionValue> Values => this.values.Values;

    /// <inheritdoc />
    public BlobDimensionValue this[string key] => this.values[key];

    /// <summary>Validates names, types, duplicates, and entry count and takes a detached snapshot.</summary>
    /// <param name="values">Explicit entries. Null is invalid; an empty enumeration clears Dimensions.</param>
    /// <returns>An immutable collection, or a validation failure.</returns>
    /// <example><code>var values = BlobDimensions.Create(new Dictionary&lt;string, object&gt; { ["FiscalYear"] = 2026 });</code></example>
    public static Result<BlobDimensions> Create(IEnumerable<KeyValuePair<string, object>> values)
    {
        if (values is null) return Invalid("Dimensions input is required.");
        var result = new Dictionary<string, BlobDimensionValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in values)
        {
            if (!BlobDimensionValues.IsValidName(item.Key)) return Invalid("Dimension name is invalid.");
            if (result.Count == BlobDimensionLimits.MaximumCount) return Invalid("Dimension count exceeds the limit.");
            var scalar = BlobDimensionValue.Create(item.Value);
            if (scalar.IsFailure) return Result<BlobDimensions>.Failure(scalar);
            if (!result.TryAdd(item.Key, scalar.Value)) return Invalid("Duplicate dimension name.");
        }

        return Result<BlobDimensions>.Success(new(result));
    }

    /// <inheritdoc />
    public bool ContainsKey(string key) => this.values.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out BlobDimensionValue value) => this.values.TryGetValue(key, out value);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, BlobDimensionValue>> GetEnumerator() => this.values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    private static Result<BlobDimensions> Invalid(string message) =>
        Result<BlobDimensions>.Failure(new ValidationError(message));
}
