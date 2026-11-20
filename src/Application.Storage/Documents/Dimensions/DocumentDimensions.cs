// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections;
using BridgingIT.DevKit.Common;

/// <summary>An immutable case-insensitive collection of explicitly supplied document dimensions.</summary>
/// <example><code>var dimensions = DocumentDimensions.Create(new Dictionary&lt;string, object&gt; { ["year"] = 2026 });</code></example>
[System.Text.Json.Serialization.JsonConverter(typeof(DocumentDimensionsJsonConverter))]
public sealed class DocumentDimensions : IReadOnlyDictionary<string, DocumentDimensionValue>
{
    private readonly Dictionary<string, DocumentDimensionValue> values;

    private DocumentDimensions(Dictionary<string, DocumentDimensionValue> values) => this.values = values;

    /// <summary>Gets an explicitly empty immutable collection.</summary>
    public static DocumentDimensions Empty { get; } = new(new(StringComparer.OrdinalIgnoreCase));

    /// <inheritdoc />
    public int Count => this.values.Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys => this.values.Keys;

    /// <inheritdoc />
    public IEnumerable<DocumentDimensionValue> Values => this.values.Values;

    /// <inheritdoc />
    public DocumentDimensionValue this[string key] => this.values[key];

    /// <summary>Validates names, types, duplicates, and entry count and takes a detached snapshot.</summary>
    /// <param name="values">Explicit entries. Null is invalid; an empty enumeration clears Dimensions.</param>
    /// <returns>An immutable collection, or a validation failure.</returns>
    /// <example><code>var values = DocumentDimensions.Create(new Dictionary&lt;string, object&gt; { ["FiscalYear"] = 2026 });</code></example>
    public static Result<DocumentDimensions> Create(IEnumerable<KeyValuePair<string, object>> values)
    {
        if (values is null) return Invalid("Dimensions input is required.");
        var result = new Dictionary<string, DocumentDimensionValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in values)
        {
            if (!DocumentDimensionValues.IsValidName(item.Key)) return Invalid("Dimension name is invalid.");
            if (result.Count == DocumentDimensionLimits.MaximumCount) return Invalid("Dimension count exceeds the limit.");
            var scalar = DocumentDimensionValue.Create(item.Value);
            if (scalar.IsFailure) return Result<DocumentDimensions>.Failure(scalar);
            if (!result.TryAdd(item.Key, scalar.Value)) return Invalid("Duplicate dimension name.");
        }

        return Result<DocumentDimensions>.Success(new(result));
    }

    /// <inheritdoc />
    public bool ContainsKey(string key) => this.values.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out DocumentDimensionValue value) => this.values.TryGetValue(key, out value);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, DocumentDimensionValue>> GetEnumerator() => this.values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    private static Result<DocumentDimensions> Invalid(string message) =>
        Result<DocumentDimensions>.Failure(new ValidationError(message));
}
