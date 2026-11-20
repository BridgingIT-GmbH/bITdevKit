// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Text;
using Application.Storage;

/// <summary>Maps validated document scalars to the three indexed search slots.</summary>
/// <example><code>var row = new StorageDocumentDimension(); DocumentDimensionValueEncoding.Encode(DocumentDimensionValue.Create(42).Value, row);</code></example>
public static class DocumentDimensionValueEncoding
{
    /// <summary>Sets the logical kind and exactly its required search slot.</summary>
    /// <param name="value">A validated immutable scalar.</param>
    /// <param name="target">The persisted value row to populate.</param>
    /// <example><code>var row = new StorageDocumentDimension(); DocumentDimensionValueEncoding.Encode(DocumentDimensionValue.Create(42).Value, row);</code></example>
    public static void Encode(DocumentDimensionValue value, StorageDocumentDimension target)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(target);
        target.ValueType = value.ValueType;
        target.IntegralValue = value.Value switch { int number => number, bool flag => flag ? 1L : 0L, DateTime date => date.Ticks, _ => null };
        target.DecimalValue = value.Value is decimal decimalNumber ? decimalNumber : null;
        target.StringValue = value.Value is string text ? Convert.ToHexString(Encoding.UTF8.GetBytes(text)) : null;
    }

    /// <summary>Validates a persisted discriminator/slot combination and returns its logical value.</summary>
    /// <param name="row">The stored row.</param>
    /// <returns>The exact logical scalar, or a validation failure for malformed stored state.</returns>
    /// <example><code>var scalar = DocumentDimensionValueEncoding.Decode(storedRow);</code></example>
    public static Result<DocumentDimensionValue> Decode(StorageDocumentDimension row)
    {
        if (row is null) return Invalid();
        var slots = (row.IntegralValue.HasValue ? 1 : 0) + (row.DecimalValue.HasValue ? 1 : 0) + (row.StringValue is not null ? 1 : 0);
        if (row.ValueType == DocumentDimensionValueType.Null)
            return slots == 0 ? DocumentDimensionValue.Create(null) : Invalid();
        if (slots != 1) return Invalid();
        object value;
        switch (row.ValueType)
        {
            case DocumentDimensionValueType.Integer when row.IntegralValue is >= int.MinValue and <= int.MaxValue:
                value = (int)row.IntegralValue.Value; break;
            case DocumentDimensionValueType.Boolean when row.IntegralValue is 0 or 1:
                value = row.IntegralValue == 1; break;
            case DocumentDimensionValueType.DateTime when row.IntegralValue >= 0 && row.IntegralValue <= DateTime.MaxValue.Ticks:
                value = new DateTime(row.IntegralValue.Value, DateTimeKind.Utc); break;
            case DocumentDimensionValueType.Decimal when row.DecimalValue.HasValue:
                value = row.DecimalValue.Value; break;
            case DocumentDimensionValueType.String when row.StringValue is not null:
                try
                {
                    if (row.StringValue.Length > 512 || row.StringValue.Any(character => !(character is >= '0' and <= '9' or >= 'A' and <= 'F')))
                        return Invalid();
                    value = new UTF8Encoding(false, true).GetString(Convert.FromHexString(row.StringValue));
                }
                catch (Exception exception) when (exception is FormatException or DecoderFallbackException) { return Invalid(); }

                break;
            default: return Invalid();
        }

        return DocumentDimensionValue.Create(value);
    }

    private static Result<DocumentDimensionValue> Invalid() => Result<DocumentDimensionValue>.Failure(new ValidationError("Stored dimension discriminator or search representation is invalid."));
}
