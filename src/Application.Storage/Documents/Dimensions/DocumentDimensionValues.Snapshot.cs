// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

public static partial class DocumentDimensionValues
{
    private static readonly UTF8Encoding SnapshotUtf8 = new(false, true);

    /// <summary>Serializes an internal compact typed snapshot; empty snapshots use database null.</summary>
    /// <remarks>This storage representation is independent of the public typed-envelope JSON format.</remarks>
    /// <example><code>var snapshot = DocumentDimensionValues.Serialize(dimensions);</code></example>
    public static string Serialize(DocumentDimensions dimensions)
    {
        if (dimensions is null || dimensions.Count == 0) return null;
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            foreach (var entry in dimensions)
            {
                writer.WritePropertyName(entry.Key);
                writer.WriteStartArray();
                writer.WriteNumberValue((byte)entry.Value.ValueType);
                switch (entry.Value.Value)
                {
                    case null: writer.WriteNullValue(); break;
                    case int integer: writer.WriteNumberValue(integer); break;
                    case bool flag: writer.WriteBooleanValue(flag); break;
                    case decimal number: writer.WriteStringValue(number.ToString("G29", CultureInfo.InvariantCulture)); break;
                    case DateTime date: writer.WriteNumberValue(date.Ticks); break;
                    case string value: writer.WriteStringValue(value); break;
                    default: throw new InvalidDataException("Invalid Document Dimensions snapshot value.");
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return SnapshotUtf8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Decodes the private snapshot while validating names, scalar kinds, bounds and duplicates.</summary>
    /// <example><code>var restored = DocumentDimensionValues.Deserialize(snapshot);</code></example>
    public static Result<DocumentDimensions> Deserialize(string json)
    {
        if (json is null) return Result<DocumentDimensions>.Success(DocumentDimensions.Empty);
        try
        {
            var reader = new Utf8JsonReader(SnapshotUtf8.GetBytes(json));
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return InvalidSnapshot();
            var entries = new List<KeyValuePair<string, object>>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName || entries.Count == DocumentDimensionLimits.MaximumCount) return InvalidSnapshot();
                var name = reader.GetString();
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray || !reader.Read()
                    || reader.TokenType != JsonTokenType.Number || !reader.TryGetByte(out var kind)
                    || kind > (byte)DocumentDimensionValueType.DateTime || !reader.Read()) return InvalidSnapshot();
                object value = (DocumentDimensionValueType)kind switch
                {
                    DocumentDimensionValueType.Null when reader.TokenType == JsonTokenType.Null => null,
                    DocumentDimensionValueType.Integer when reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var integer) => integer,
                    DocumentDimensionValueType.Boolean when reader.TokenType is JsonTokenType.True or JsonTokenType.False => reader.GetBoolean(),
                    DocumentDimensionValueType.Decimal when reader.TokenType == JsonTokenType.String => DocumentDimensionsJsonConverter.ReadDecimal(reader.GetString()),
                    DocumentDimensionValueType.String when reader.TokenType == JsonTokenType.String => reader.GetString(),
                    DocumentDimensionValueType.DateTime when reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var ticks)
                        && ticks >= 0 && ticks <= DateTime.MaxValue.Ticks => new DateTime(ticks, DateTimeKind.Utc),
                    _ => throw new JsonException("Invalid snapshot scalar.")
                };
                if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray) return InvalidSnapshot();
                entries.Add(new(name, value));
            }

            if (reader.TokenType != JsonTokenType.EndObject || reader.Read()) return InvalidSnapshot();
            return DocumentDimensions.Create(entries);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or ArgumentException)
        {
            return InvalidSnapshot();
        }
    }

    private static Result<DocumentDimensions> InvalidSnapshot() =>
        Result<DocumentDimensions>.Failure(new ValidationError("Dimension snapshot is invalid."));
}
