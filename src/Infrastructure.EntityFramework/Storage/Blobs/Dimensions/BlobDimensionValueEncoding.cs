// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Text;
using Application.Storage;

/// <summary>Maps validated blob scalars to the three indexed search slots.</summary>
/// <example><code>var row = new StorageBlobDimension(); BlobDimensionValueEncoding.Encode(BlobDimensionValue.Create(42).Value, row);</code></example>
public static class BlobDimensionValueEncoding
{
    /// <summary>Sets the logical kind and exactly its required search slot.</summary>
    /// <param name="value">A validated immutable scalar.</param>
    /// <param name="target">The persisted value row to populate.</param>
    /// <example><code>var row = new StorageBlobDimension(); BlobDimensionValueEncoding.Encode(BlobDimensionValue.Create(42).Value, row);</code></example>
    public static void Encode(BlobDimensionValue value, StorageBlobDimension target)
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
    /// <example><code>var scalar = BlobDimensionValueEncoding.Decode(storedRow);</code></example>
    public static Result<BlobDimensionValue> Decode(StorageBlobDimension row)
    {
        if (row is null) return Invalid();
        var slots = (row.IntegralValue.HasValue ? 1 : 0) + (row.DecimalValue.HasValue ? 1 : 0) + (row.StringValue is not null ? 1 : 0);
        if (row.ValueType == BlobDimensionValueType.Null)
            return slots == 0 ? BlobDimensionValue.Create(null) : Invalid();
        if (slots != 1) return Invalid();
        object value;
        switch (row.ValueType)
        {
            case BlobDimensionValueType.Integer when row.IntegralValue is >= int.MinValue and <= int.MaxValue:
                value = (int)row.IntegralValue.Value; break;
            case BlobDimensionValueType.Boolean when row.IntegralValue is 0 or 1:
                value = row.IntegralValue == 1; break;
            case BlobDimensionValueType.DateTime when row.IntegralValue >= 0 && row.IntegralValue <= DateTime.MaxValue.Ticks:
                value = new DateTime(row.IntegralValue.Value, DateTimeKind.Utc); break;
            case BlobDimensionValueType.Decimal when row.DecimalValue.HasValue:
                value = row.DecimalValue.Value; break;
            case BlobDimensionValueType.String when row.StringValue is not null:
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

        return BlobDimensionValue.Create(value);
    }

    private static Result<BlobDimensionValue> Invalid() => Result<BlobDimensionValue>.Failure(new ValidationError("Stored dimension discriminator or search representation is invalid."));
}
