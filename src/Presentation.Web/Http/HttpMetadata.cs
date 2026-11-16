// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>Converts HTTP JSON metadata into scalar properties and expiration changes.</summary>
/// <example><code>var properties = HttpMetadata.ParseProperties("{\"reviewed\":true}");</code></example>
public static class HttpMetadata
{
    /// <summary>Parses a scalar property object; omitted metadata is empty unless preservation is requested.</summary>
    /// <example><code>var properties = HttpMetadata.ParseProperties(header);</code></example>
    public static Result<PropertyBag> ParseProperties(string json, bool preserveMissing = false)
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

            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Properties must be an object.");
            var properties = new PropertyBag();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(property.Name)) throw new JsonException("Property names must not be blank.");
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
        if (value.TryGetInt64(out var integer)) return integer;
        if (!value.TryGetDouble(out var floating) || !double.IsFinite(floating) || (floating == 0 && HasNonzeroSignificand(value)))
        {
            throw new JsonException("Property numbers must be finite and within the supported numeric range.");
        }

        if (value.TryGetDecimal(out var number) && (double)number == floating) return number;
        return floating;
    }

    private static bool HasNonzeroSignificand(JsonElement value)
    {
        foreach (var character in value.GetRawText())
        {
            if (character is 'e' or 'E') break;
            if (character is >= '1' and <= '9') return true;
        }

        return false;
    }

    /// <summary>Resolves preserve, set, and clear expiration requests without ambiguous combinations.</summary>
    /// <example><code>var expiration = HttpMetadata.ResolveExpiration(null, true);</code></example>
    public static Result<ExpirationChange> ResolveExpiration(DateTimeOffset? expiresAt, bool clearExpiration) =>
        clearExpiration && expiresAt.HasValue
            ? Result<ExpirationChange>.Failure(new ValidationError("ClearExpiration and ExpiresAt cannot be combined."))
            : Result<ExpirationChange>.Success(clearExpiration ? ExpirationChange.Clear : expiresAt.HasValue ? ExpirationChange.At(expiresAt.Value) : ExpirationChange.Preserve);
}
