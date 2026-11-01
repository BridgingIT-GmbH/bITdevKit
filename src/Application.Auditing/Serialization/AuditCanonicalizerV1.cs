// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

using System.Buffers;
using System.Security.Cryptography;

/// <summary>Version-one compact UTF-8 canonicalization with ordinal keys and preserved array/numeric order.</summary>
/// <example><code>var bytes = AuditCanonicalizerV1.Encode(document.RootElement);</code></example>
public static class AuditCanonicalizerV1
{
    /// <summary>Canonicalizes JSON without activating application types or claiming RFC 8785 equivalence.</summary>
    /// <example><code>var bytes = AuditCanonicalizerV1.Encode(json);</code></example>
    public static byte[] Encode(JsonElement value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer)) Write(writer, value);
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Returns the lowercase SHA-256 content identity.</summary>
    /// <example><code>var hash = AuditCanonicalizerV1.Hash(bytes);</code></example>
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!names.Add(property.Name)) throw new JsonException("Duplicate audit JSON property.");
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) Write(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.Number: writer.WriteRawValue(value.GetRawText(), skipInputValidation: false); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new JsonException("Undefined audit JSON value.");
        }
    }
}
