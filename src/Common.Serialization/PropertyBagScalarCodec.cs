// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using System.Text;
using System.Text.Json;

/// <summary>
/// Encodes scalar property-bag values into versioned provider-safe strings.
/// </summary>
/// <example>
/// <code>
/// var encoded = PropertyBagScalarCodec.Encode("true");
/// var decoded = PropertyBagScalarCodec.Decode(encoded);
/// </code>
/// </example>
public static class PropertyBagScalarCodec
{
    /// <summary>
    /// Gets the prefix identifying encoded scalar values.
    /// </summary>
    /// <example>
    /// <code>
    /// var prefix = PropertyBagScalarCodec.Prefix;
    /// </code>
    /// </example>
    public const string Prefix = "bdk_v1_";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Encodes a supported scalar value.
    /// </summary>
    /// <param name="value">The scalar value.</param>
    /// <returns>A versioned Base64Url string.</returns>
    /// <exception cref="ArgumentException">The value type is not supported.</exception>
    /// <example>
    /// <code>
    /// var encoded = PropertyBagScalarCodec.Encode(42);
    /// </code>
    /// </example>
    public static string Encode(object value)
    {
        var scalar = value switch
        {
            null => new EncodedScalar("null", null),
            string item => new EncodedScalar("string", item),
            char item => new EncodedScalar("char", item.ToString()),
            bool item => new EncodedScalar("bool", item ? "true" : "false"),
            byte item => Number("byte", item),
            sbyte item => Number("sbyte", item),
            short item => Number("int16", item),
            ushort item => Number("uint16", item),
            int item => Number("int32", item),
            uint item => Number("uint32", item),
            long item => Number("int64", item),
            ulong item => Number("uint64", item),
            float item => new EncodedScalar("single", item.ToString("R", CultureInfo.InvariantCulture)),
            double item => new EncodedScalar("double", item.ToString("R", CultureInfo.InvariantCulture)),
            decimal item => Number("decimal", item),
            Guid item => new EncodedScalar("guid", item.ToString("D")),
            DateTime item => new EncodedScalar("datetime", item.ToString("O", CultureInfo.InvariantCulture)),
            DateTimeOffset item => new EncodedScalar("datetimeoffset", item.ToString("O", CultureInfo.InvariantCulture)),
            DateOnly item => new EncodedScalar("dateonly", item.ToString("O", CultureInfo.InvariantCulture)),
            TimeOnly item => new EncodedScalar("timeonly", item.ToString("O", CultureInfo.InvariantCulture)),
            TimeSpan item => new EncodedScalar("timespan", item.ToString("c", CultureInfo.InvariantCulture)),
            byte[] item => new EncodedScalar("bytes", Convert.ToBase64String(item)),
            _ => throw new ArgumentException(
                $"Property value type '{value.GetType().FullName}' is not a supported scalar type.",
                nameof(value))
        };

        return Prefix + Base64UrlHelper.Encode(JsonSerializer.SerializeToUtf8Bytes(scalar, JsonOptions));
    }

    /// <summary>
    /// Decodes a scalar value. Unprefixed legacy values are returned as strings.
    /// </summary>
    /// <param name="value">The encoded or legacy value.</param>
    /// <returns>The decoded scalar.</returns>
    /// <exception cref="FormatException">The encoded value is malformed or has an unsupported discriminator.</exception>
    /// <example>
    /// <code>
    /// var decoded = PropertyBagScalarCodec.Decode(encoded);
    /// </code>
    /// </example>
    public static object Decode(string value)
    {
        if (value is null || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        try
        {
            var scalar = JsonSerializer.Deserialize<EncodedScalar>(Base64UrlHelper.Decode(value[Prefix.Length..]), JsonOptions)
                ?? throw new FormatException("Encoded property value is empty.");
            return scalar.Type switch
            {
                "null" => null,
                "string" => scalar.Value ?? string.Empty,
                "char" when scalar.Value?.Length == 1 => scalar.Value[0],
                "bool" => bool.Parse(scalar.Value),
                "byte" => byte.Parse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "sbyte" => sbyte.Parse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "int16" => short.Parse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "uint16" => ushort.Parse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "int32" => int.Parse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "uint32" => uint.Parse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "int64" => long.Parse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "uint64" => ulong.Parse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "single" => float.Parse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture),
                "double" => double.Parse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture),
                "decimal" => decimal.Parse(scalar.Value, NumberStyles.Number, CultureInfo.InvariantCulture),
                "guid" => Guid.ParseExact(scalar.Value, "D"),
                "datetime" => DateTime.Parse(scalar.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                "datetimeoffset" => DateTimeOffset.Parse(scalar.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                "dateonly" => DateOnly.ParseExact(scalar.Value, "O", CultureInfo.InvariantCulture),
                "timeonly" => TimeOnly.ParseExact(scalar.Value, "O", CultureInfo.InvariantCulture),
                "timespan" => TimeSpan.ParseExact(scalar.Value, "c", CultureInfo.InvariantCulture),
                "bytes" => Convert.FromBase64String(scalar.Value),
                _ => throw new FormatException($"Encoded property scalar type '{scalar.Type}' is not supported.")
            };
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or OverflowException)
        {
            throw new FormatException("Encoded property value is invalid.", exception);
        }
    }

    private static EncodedScalar Number(string type, IFormattable value) =>
        new(type, value.ToString(null, CultureInfo.InvariantCulture));

    private sealed record EncodedScalar(string Type, string Value);
}
