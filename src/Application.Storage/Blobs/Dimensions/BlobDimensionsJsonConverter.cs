// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Reads and writes explicitly typed blob Dimensions without numeric type loss.</summary>
/// <example><code>var json = System.Text.Json.JsonSerializer.Serialize(dimensions);</code></example>
public sealed class BlobDimensionsJsonConverter : JsonConverter<BlobDimensions>
{
    /// <inheritdoc />
    public override BlobDimensions Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw Invalid();
        var entries = new List<KeyValuePair<string, object>>();
        try
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName || entries.Count == BlobDimensionLimits.MaximumCount) throw Invalid();
                var name = reader.GetString();
                if (!reader.Read()) throw Invalid();
                entries.Add(new(name, ReadScalar(ref reader)));
            }

            if (reader.TokenType != JsonTokenType.EndObject) throw Invalid();
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException or ArgumentException)
        {
            throw Invalid();
        }

        var result = BlobDimensions.Create(entries);
        return result.IsSuccess ? result.Value : throw Invalid();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, BlobDimensions value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var entry in value)
        {
            writer.WritePropertyName(entry.Key);
            writer.WriteStartObject();
            writer.WriteString("type", entry.Value.ValueType switch
            {
                BlobDimensionValueType.Integer => "integer",
                BlobDimensionValueType.Boolean => "boolean",
                BlobDimensionValueType.Decimal => "decimal",
                BlobDimensionValueType.String => "string",
                BlobDimensionValueType.DateTime => "datetime",
                _ => "null"
            });
            switch (entry.Value.Value)
            {
                case null: writer.WriteNull("value"); break;
                case int integer: writer.WriteNumber("value", integer); break;
                case bool flag: writer.WriteBoolean("value", flag); break;
                case decimal number: writer.WriteString("value", number.ToString("G29", CultureInfo.InvariantCulture)); break;
                case DateTime date: writer.WriteString("value", date.ToString("O", CultureInfo.InvariantCulture)); break;
                case string text: writer.WriteString("value", text); break;
                default: throw Invalid();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>Reads one explicitly typed Dimension operand from a filter criterion.</summary>
    public static object ReadFilterOperand(JsonElement value)
    {
        try
        {
            var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(value.GetRawText()));
            reader.Read();
            return ReadScalar(ref reader);
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException or ArgumentException)
        {
            throw Invalid();
        }
    }

    private static object ReadScalar(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw Invalid();
        var kind = BlobDimensionValueType.Null;
        var kindSeen = false;
        var valueSeen = false;
        Utf8JsonReader value = default;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) throw Invalid();
            if (reader.ValueTextEquals("type"u8) && !kindSeen)
            {
                if (!reader.Read() || reader.TokenType != JsonTokenType.String) throw Invalid();
                kind = reader.ValueTextEquals("null"u8) ? BlobDimensionValueType.Null
                    : reader.ValueTextEquals("integer"u8) ? BlobDimensionValueType.Integer
                    : reader.ValueTextEquals("boolean"u8) ? BlobDimensionValueType.Boolean
                    : reader.ValueTextEquals("string"u8) ? BlobDimensionValueType.String
                    : reader.ValueTextEquals("decimal"u8) ? BlobDimensionValueType.Decimal
                    : reader.ValueTextEquals("datetime"u8) ? BlobDimensionValueType.DateTime : throw Invalid();
                kindSeen = true;
            }
            else if (reader.ValueTextEquals("value"u8) && !valueSeen)
            {
                if (!reader.Read()) throw Invalid();
                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray) throw Invalid();
                value = reader;
                valueSeen = true;
            }
            else throw Invalid();
        }

        if (reader.TokenType != JsonTokenType.EndObject || !kindSeen || !valueSeen) throw Invalid();
        return kind switch
        {
            BlobDimensionValueType.Null when value.TokenType == JsonTokenType.Null => null,
            BlobDimensionValueType.Integer when value.TokenType == JsonTokenType.Number && value.TryGetInt32(out var integer) => integer,
            BlobDimensionValueType.Boolean when value.TokenType is JsonTokenType.True or JsonTokenType.False => value.GetBoolean(),
            BlobDimensionValueType.String when value.TokenType == JsonTokenType.String => value.GetString(),
            BlobDimensionValueType.Decimal when value.TokenType == JsonTokenType.String => ReadDecimal(value.GetString()),
            BlobDimensionValueType.DateTime when value.TokenType == JsonTokenType.String => ReadDateTime(value.GetString()),
            _ => throw Invalid()
        };
    }

    internal static decimal ReadDecimal(string text)
    {
        if (string.IsNullOrEmpty(text)) throw Invalid();
        // G29 uses scientific notation for small decimals in stored snapshots and typed JSON.
        if (text.Contains('E') && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var scientific)
            && string.Equals(text, scientific.ToString("G29", CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return scientific;
        }

        var unsigned = text[0] is '-' or '+' ? text[1..] : text;
        var parts = unsigned.Split('.');
        if (parts.Length > 2 || parts.Any(part => part.Length == 0 || part.Any(character => character is < '0' or > '9'))
            || parts[0].TrimStart('0').Length > 12 || parts.Length == 2 && parts[1].TrimEnd('0').Length > 6
            || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) throw Invalid();
        return value;
    }

    private static DateTime ReadDateTime(string text) => DateTime.TryParseExact(text,
        ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"], CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date) ? date : throw Invalid();

    private static JsonException Invalid() => new("Dimensions must contain valid names and explicitly typed scalar envelopes within their limits.");
}
