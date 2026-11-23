// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;

/// <summary>Parses a plain JSON object into a case-insensitive bag of scalar values.</summary>
/// <example>
/// <code>
/// var result = ScalarPropertyBagJson.Parse("""{"sampleCount":42,"reviewed":true}""");
/// if (result.IsSuccess)
/// {
///     Console.WriteLine(result.Value.Get("sampleCount"));
/// }
/// </code>
/// </example>
public static class ScalarPropertyBagJson
{
    /// <summary>Reads JSON strings, Boolean values, nulls, and numbers into a new property bag.</summary>
    /// <param name="json">A JSON object, a JSON null, or omitted text.</param>
    /// <param name="preserveMissing">
    /// Return a null bag for omitted, blank, or JSON null input when true. Otherwise return an empty bag.
    /// An explicit empty object always returns an empty bag.
    /// </param>
    /// <returns>
    /// The parsed bag or the requested missing value on success. A <see cref="ValidationError"/>
    /// reports JSON syntax errors, non-object roots other than null, blank property names, nested values,
    /// or numbers outside the supported range.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Decoding a property name or string value encounters an unpaired surrogate escape.
    /// </exception>
    /// <remarks>
    /// Numbers use <see cref="long"/> when possible, then <see cref="decimal"/> if its conversion to
    /// <see cref="double"/> agrees with the parsed double, and otherwise a finite <see cref="double"/>.
    /// Nonzero numbers that round to zero as a double are rejected. Case-insensitive duplicate keys
    /// keep the first spelling and last value.
    /// </remarks>
    public static Result<PropertyBag> Parse(string json, bool preserveMissing = false)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<PropertyBag>.Success(preserveMissing ? null : new PropertyBag());
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Null)
            {
                return Result<PropertyBag>.Success(preserveMissing ? null : new PropertyBag());
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Properties must be an object.");
            }

            var properties = new PropertyBag();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(property.Name))
                {
                    throw new JsonException("Property names must not be blank.");
                }

                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number => ParseNumber(property.Value),
                    _ => throw new JsonException("Property values must be JSON scalars.")
                };
                properties.Set(property.Name, value);
            }

            return Result<PropertyBag>.Success(properties);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException)
        {
            return Result<PropertyBag>.Failure(new ValidationError(ex.Message));
        }
    }

    private static object ParseNumber(JsonElement value)
    {
        if (value.TryGetInt64(out var integer))
        {
            return integer;
        }

        if (!value.TryGetDouble(out var floating) || !double.IsFinite(floating) || (floating == 0 && HasNonzeroSignificand(value)))
        {
            throw new JsonException("Property numbers must be finite and within the supported numeric range.");
        }

        if (value.TryGetDecimal(out var number) && (double)number == floating)
        {
            return number;
        }

        return floating;
    }

    private static bool HasNonzeroSignificand(JsonElement value)
    {
        foreach (var character in value.GetRawText())
        {
            if (character is 'e' or 'E')
            {
                break;
            }

            if (character is >= '1' and <= '9')
            {
                return true;
            }
        }

        return false;
    }
}
