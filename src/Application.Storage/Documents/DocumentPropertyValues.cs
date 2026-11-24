// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Validates, copies, and persists scalar application properties for Document Storage.</summary>
/// <example><code>var copy = DocumentPropertyValues.Clone(properties);</code></example>
public static class DocumentPropertyValues
{
    /// <summary>Validates an optional property bag against the scalar codec domain.</summary>
    /// <param name="properties">The optional application properties.</param>
    /// <returns>A typed validation failure for unsupported values.</returns>
    /// <example><code>var result = DocumentPropertyValues.Validate(properties);</code></example>
    public static Result Validate(PropertyBag properties)
    {
        try
        {
            if (properties is not null)
            {
                foreach (var item in properties)
                {
                    _ = PropertyBagScalarCodec.Encode(item.Value);
                }
            }

            return Result.Success();
        }
        catch (ArgumentException exception)
        {
            return Result.Failure(new DocumentStoreSerializationError("Unsupported property value.", exception));
        }
    }

    /// <summary>Copies an optional scalar bag, including mutable byte-array values.</summary>
    /// <param name="properties">The bag to copy; null means preserve on input.</param>
    /// <returns>An independent bag, or null when input is null.</returns>
    /// <example><code>var copy = DocumentPropertyValues.Clone(properties);</code></example>
    public static PropertyBag Clone(PropertyBag properties) => PropertyBagSerialization.Clone(properties);

    /// <summary>Encodes scalar type information into a JSON dictionary for persistence.</summary>
    /// <param name="properties">The application or private metadata bag.</param>
    /// <returns>Encoded JSON, or null for an empty bag.</returns>
    /// <example><code>var json = DocumentPropertyValues.Serialize(properties);</code></example>
    public static string Serialize(PropertyBag properties) => PropertyBagSerialization.Serialize(properties);

    /// <summary>Decodes the scalar-preserving persisted dictionary.</summary>
    /// <param name="json">The persisted JSON, or null for an empty bag.</param>
    /// <returns>An independent decoded bag.</returns>
    /// <example><code>var properties = DocumentPropertyValues.Deserialize(json);</code></example>
    public static PropertyBag Deserialize(string json) => PropertyBagSerialization.Deserialize(json);
}
