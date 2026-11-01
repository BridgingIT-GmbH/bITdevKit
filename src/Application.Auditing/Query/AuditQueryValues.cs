// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>Copies approved scalar inputs into bounded, invariant query values.</summary>
/// <example><code>var value = AuditQueryValues.Convert("12.50", "Decimal");</code></example>
public static class AuditQueryValues
{
    private const int MAX_VALUE_BYTES = 4096;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly BigInteger MaxDecimalCoefficient = (BigInteger.One << 96) - 1;
    private static readonly string[] TimestampFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"
    ];

    /// <summary>Converts a scalar without invoking business-object conversion or retaining JSON leases.</summary>
    /// <param name="value">A supported CLR or JSON scalar, or an actual null.</param>
    /// <param name="kind">String, Guid, Integer, Decimal, Boolean, or Timestamp.</param>
    /// <returns>A copied value or a payload-free validation error.</returns>
    /// <example><code>var queryValue = AuditQueryValues.Convert(DateTimeOffset.UtcNow, "Timestamp");</code></example>
    public static Result<AuditQueryValue> Convert(object value, string kind)
    {
        if (kind is not ("String" or "Guid" or "Integer" or "Decimal" or "Boolean" or "Timestamp"))
        {
            return Invalid();
        }

        try
        {
            if (value is JsonElement json)
            {
                value = json.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => json.GetString(),
                    JsonValueKind.Number when kind is "Integer" or "Decimal" => json.GetRawText(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => json
                };
            }

            if (value is null)
            {
                return Result<AuditQueryValue>.Success(new AuditQueryValue());
            }

            if (value is string text && !WithinLimit(text))
            {
                return Invalid();
            }

            var converted = kind switch
            {
                "String" => StringValue(value),
                "Guid" => GuidValue(value),
                "Integer" or "Decimal" => NumericValue(value, kind),
                "Boolean" => BooleanValue(value),
                "Timestamp" => TimestampValue(value),
                _ => null
            };

            return converted is null ? Invalid() : Result<AuditQueryValue>.Success(converted);
        }
        catch (Exception exception) when (exception is InvalidOperationException or EncoderFallbackException or FormatException or OverflowException)
        {
            return Invalid();
        }
    }

    private static AuditQueryValue StringValue(object value)
    {
        var text = value switch
        {
            string scalar => scalar,
            Guid identity => identity.ToString("D"),
            Enum enumeration => enumeration.ToString(),
            _ => null
        };

        return text is not null && WithinLimit(text) ? new AuditQueryValue { Kind = "String", Text = text } : null;
    }

    private static AuditQueryValue GuidValue(object value)
    {
        if (value is Guid identity)
        {
            return new AuditQueryValue { Kind = "Guid", Text = identity.ToString("D") };
        }

        return value is string text && Guid.TryParse(text, out identity)
            ? new AuditQueryValue { Kind = "Guid", Text = identity.ToString("D") }
            : null;
    }

    private static AuditQueryValue BooleanValue(object value)
    {
        if (value is bool boolean)
        {
            return new AuditQueryValue { Kind = "Boolean", Boolean = boolean };
        }

        return value is string text && bool.TryParse(text, out boolean)
            ? new AuditQueryValue { Kind = "Boolean", Boolean = boolean }
            : null;
    }

    private static AuditQueryValue TimestampValue(object value)
    {
        var timestamp = value switch
        {
            DateTimeOffset scalar => scalar,
            DateTime { Kind: DateTimeKind.Utc } scalar => new DateTimeOffset(scalar),
            string text when DateTimeOffset.TryParseExact(text, TimestampFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) => parsed,
            _ => (DateTimeOffset?)null
        };

        return timestamp.HasValue ? new AuditQueryValue { Kind = "Timestamp", Ticks = timestamp.Value.UtcTicks } : null;
    }

    private static AuditQueryValue NumericValue(object value, string kind)
    {
        var text = value switch
        {
            string scalar => scalar,
            sbyte or byte or short or ushort or int or uint or long or ulong or decimal => System.Convert.ToString(value, CultureInfo.InvariantCulture),
            Int128 scalar => scalar.ToString(CultureInfo.InvariantCulture),
            UInt128 scalar => scalar.ToString(CultureInfo.InvariantCulture),
            float scalar when float.IsFinite(scalar) => scalar.ToString("R", CultureInfo.InvariantCulture),
            double scalar when double.IsFinite(scalar) => scalar.ToString("R", CultureInfo.InvariantCulture),
            _ => null
        };

        if (text is null || !TryExactDecimal(text, out var number))
        {
            return null;
        }

        if (kind == "Decimal")
        {
            return new AuditQueryValue { Kind = kind, Number = number };
        }

        return number >= long.MinValue && number <= long.MaxValue && decimal.Truncate(number) == number
            ? new AuditQueryValue { Kind = kind, Number = number, Ticks = (long)number }
            : null;
    }

    private static bool TryExactDecimal(string text, out decimal value)
    {
        value = 0;
        if (text.Length is 0 or > MAX_VALUE_BYTES)
        {
            return false;
        }

        var index = 0;
        var negative = text[0] == '-';
        if (text[0] is '+' or '-')
        {
            index++;
        }

        var coefficient = BigInteger.Zero;
        var digits = 0;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            coefficient = (coefficient * 10) + (text[index++] - '0');
            digits++;
        }

        if (digits == 0)
        {
            return false;
        }

        var scale = 0;
        if (index < text.Length && text[index] == '.')
        {
            index++;
            while (index < text.Length && char.IsAsciiDigit(text[index]))
            {
                coefficient = (coefficient * 10) + (text[index++] - '0');
                scale++;
            }

            if (scale == 0)
            {
                return false;
            }
        }

        if (index < text.Length && text[index] is 'e' or 'E')
        {
            index++;
            var exponentStart = index;
            if (index < text.Length && text[index] is '+' or '-')
            {
                index++;
            }

            var exponentDigits = index;
            while (index < text.Length && char.IsAsciiDigit(text[index]))
            {
                index++;
            }

            if (index == exponentDigits || !int.TryParse(text.AsSpan(exponentStart, index - exponentStart),
                NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var exponent) || exponent is < -8192 or > 8192)
            {
                return false;
            }

            scale -= exponent;
        }

        if (index != text.Length)
        {
            return false;
        }

        if (coefficient.IsZero)
        {
            return true;
        }

        while (scale > 0 && coefficient % 10 == 0)
        {
            coefficient /= 10;
            scale--;
        }

        if (scale > 28 || scale < -28)
        {
            return false;
        }

        if (scale < 0)
        {
            coefficient *= BigInteger.Pow(10, -scale);
            scale = 0;
        }

        if (coefficient > MaxDecimalCoefficient)
        {
            return false;
        }

        value = (decimal)coefficient;
        for (var position = 0; position < scale; position++)
        {
            value /= 10m;
        }

        if (negative)
        {
            value = -value;
        }

        return true;
    }

    private static bool WithinLimit(string value) => value.Length <= MAX_VALUE_BYTES && StrictUtf8.GetByteCount(value) <= MAX_VALUE_BYTES;

    private static Result<AuditQueryValue> Invalid() => Result<AuditQueryValue>.Failure(new AuditError("AuditQueryValueInvalid"));
}
