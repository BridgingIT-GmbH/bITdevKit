// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Text;
using BridgingIT.DevKit.Common;

/// <summary>An immutable validated scalar blob dimension.</summary>
/// <example><code>var value = BlobDimensionValue.Create(42);</code></example>
public sealed record BlobDimensionValue
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private BlobDimensionValue(BlobDimensionValueType valueType, object value)
    {
        this.ValueType = valueType;
        this.Value = value;
    }

    /// <summary>Gets the logical kind, independent of its physical search slot.</summary>
    public BlobDimensionValueType ValueType { get; }

    /// <summary>Gets the immutable scalar, or null for explicit null.</summary>
    public object Value { get; }

    /// <summary>Validates and creates a scalar without coercion or rounding.</summary>
    /// <param name="value">An Int32, decimal, Boolean, string, UTC DateTime, or null.</param>
    /// <returns>The validated scalar, or a validation failure.</returns>
    /// <example><code>var result = BlobDimensionValue.Create(12.50m);</code></example>
    public static Result<BlobDimensionValue> Create(object value)
    {
        BlobDimensionValueType kind;
        switch (value)
        {
            case null: kind = BlobDimensionValueType.Null; break;
            case int: kind = BlobDimensionValueType.Integer; break;
            case bool: kind = BlobDimensionValueType.Boolean; break;
            case decimal number when number >= -BlobDimensionLimits.MaximumDecimal
                && number <= BlobDimensionLimits.MaximumDecimal && decimal.Round(number, 6) == number:
                kind = BlobDimensionValueType.Decimal; break;
            case DateTime date when date.Kind == DateTimeKind.Utc:
                kind = BlobDimensionValueType.DateTime; break;
            case string text:
                try
                {
                    if (StrictUtf8.GetByteCount(text) > BlobDimensionLimits.MaximumStringBytes)
                        return Invalid();
                }
                catch (EncoderFallbackException) { return Invalid(); }

                kind = BlobDimensionValueType.String; break;
            default: return Invalid();
        }

        return Result<BlobDimensionValue>.Success(new(kind, value));
    }

    private static Result<BlobDimensionValue> Invalid() =>
        Result<BlobDimensionValue>.Failure(new ValidationError("Dimension scalar type, range, precision, Unicode, or UTC kind is invalid."));
}
