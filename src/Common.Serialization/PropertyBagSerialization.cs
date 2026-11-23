// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;

/// <summary>
/// Copies and persists scalar <see cref="PropertyBag"/> instances using the format defined by
/// <see cref="PropertyBagScalarCodec"/> for each stored value.
/// </summary>
/// <example>
/// <code>
/// var inspection = new PropertyBag { ["SampleCount"] = (ushort)42 };
/// var json = PropertyBagSerialization.Serialize(inspection);
/// var restored = PropertyBagSerialization.Deserialize(json);
/// var sampleCount = (ushort)restored.Get("sampleCount");
/// </code>
/// </example>
public static class PropertyBagSerialization
{
    /// <summary>
    /// Copies the bag and its byte-array contents, retaining key spelling and case-insensitive lookup.
    /// </summary>
    /// <param name="properties">The optional bag to copy. Values other than byte arrays are kept as supplied.</param>
    /// <returns>A new bag, including for an empty input bag, or <see langword="null"/> for null input.</returns>
    /// <example><code>var snapshot = PropertyBagSerialization.Clone(inspection);</code></example>
    public static PropertyBag Clone(PropertyBag properties) => properties is null
        ? null
        : new PropertyBag(properties.ToDictionary(
            item => item.Key,
            item => item.Value is byte[] bytes ? bytes.ToArray() : item.Value,
            StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Serializes the bag as a JSON dictionary whose string values encode their original scalar types.
    /// </summary>
    /// <param name="properties">The optional bag of values supported by <see cref="PropertyBagScalarCodec"/>.</param>
    /// <returns>The JSON dictionary, or <see langword="null"/> for a null or empty bag.</returns>
    /// <exception cref="ArgumentException">A value has an unsupported type.</exception>
    /// <example><code>var json = PropertyBagSerialization.Serialize(inspection);</code></example>
    public static string Serialize(PropertyBag properties) => properties is null || properties.Count == 0
        ? null
        : JsonSerializer.Serialize(properties.ToDictionary(
            item => item.Key,
            item => PropertyBagScalarCodec.Encode(item.Value)));

    /// <summary>
    /// Reads the persisted dictionary into a case-insensitive bag, restoring encoded scalar types and retaining
    /// unprefixed legacy values as strings.
    /// </summary>
    /// <param name="json">The persisted JSON dictionary, or null or empty text.</param>
    /// <returns>A new bag, empty when <paramref name="json"/> is null, empty, or an empty JSON object.</returns>
    /// <exception cref="JsonException">The input is invalid JSON or cannot be read as a dictionary of strings.</exception>
    /// <exception cref="ArgumentNullException">The input is the JSON literal <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The dictionary contains keys that differ only by case.</exception>
    /// <exception cref="FormatException">An encoded scalar is invalid.</exception>
    /// <example><code>var restored = PropertyBagSerialization.Deserialize(json);</code></example>
    public static PropertyBag Deserialize(string json) => string.IsNullOrEmpty(json)
        ? new PropertyBag()
        : new PropertyBag(JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            .ToDictionary(
                item => item.Key,
                item => PropertyBagScalarCodec.Decode(item.Value),
                StringComparer.OrdinalIgnoreCase));
}
