// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Text;
using BridgingIT.DevKit.Common;

/// <summary>An immutable validated scalar document dimension.</summary>
/// <example><code>var value = DocumentDimensionValue.Create(42);</code></example>
public sealed record DocumentDimensionValue
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private DocumentDimensionValue(DocumentDimensionValueType valueType, object value)
    {
        this.ValueType = valueType;
        this.Value = value;
    }

    /// <summary>Gets the logical kind, independent of its physical search slot.</summary>
    public DocumentDimensionValueType ValueType { get; }

    /// <summary>Gets the immutable scalar, or null for explicit null.</summary>
    public object Value { get; }

    /// <summary>Validates and creates a scalar without coercion or rounding.</summary>
    /// <param name="value">An Int32, decimal, Boolean, string, UTC DateTime, or null.</param>
    /// <returns>The validated scalar, or a validation failure.</returns>
    /// <example><code>var result = DocumentDimensionValue.Create(12.50m);</code></example>
    public static Result<DocumentDimensionValue> Create(object value)
    {
        DocumentDimensionValueType kind;
        switch (value)
        {
            case null: kind = DocumentDimensionValueType.Null; break;
            case int: kind = DocumentDimensionValueType.Integer; break;
            case bool: kind = DocumentDimensionValueType.Boolean; break;
            case decimal number when number >= -DocumentDimensionLimits.MaximumDecimal
                && number <= DocumentDimensionLimits.MaximumDecimal && decimal.Round(number, 6) == number:
                kind = DocumentDimensionValueType.Decimal; break;
            case DateTime date when date.Kind == DateTimeKind.Utc:
                kind = DocumentDimensionValueType.DateTime; break;
            case string text:
                try
                {
                    if (StrictUtf8.GetByteCount(text) > DocumentDimensionLimits.MaximumStringBytes)
                        return Invalid();
                }
                catch (EncoderFallbackException) { return Invalid(); }

                kind = DocumentDimensionValueType.String; break;
            default: return Invalid();
        }

        return Result<DocumentDimensionValue>.Success(new(kind, value));
    }

    private static Result<DocumentDimensionValue> Invalid() =>
        Result<DocumentDimensionValue>.Failure(new ValidationError("Dimension scalar type, range, precision, Unicode, or UTC kind is invalid."));
}
