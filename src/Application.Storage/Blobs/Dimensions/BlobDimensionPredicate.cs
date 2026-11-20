// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>An immutable validated predicate over one logical dimension name.</summary>
/// <example><code>var predicate = BlobDimensionPredicate.Create("FiscalYear", BlobDimensionOperator.Equal, 2026);</code></example>
public sealed class BlobDimensionPredicate
{
    private BlobDimensionPredicate(string name, BlobDimensionOperator operation, BlobDimensionValue operand)
    {
        this.Name = name;
        this.Operator = operation;
        this.Operand = operand;
    }

    /// <summary>Gets the logical name.</summary>
    public string Name { get; }

    /// <summary>Gets the operator.</summary>
    public BlobDimensionOperator Operator { get; }

    /// <summary>Gets the typed operand, or null for state predicates.</summary>
    public BlobDimensionValue Operand { get; }

    /// <summary>Creates a predicate; state operators omit an operand and comparisons require non-null values.</summary>
    /// <example><code>var nullCheck = BlobDimensionPredicate.Create("Approved", BlobDimensionOperator.IsNull);</code></example>
    public static Result<BlobDimensionPredicate> Create(string name, BlobDimensionOperator operation, object operand = null)
    {
        if (!BlobDimensionValues.IsValidName(name) || !Enum.IsDefined(operation)) return Invalid();
        if (operation >= BlobDimensionOperator.Exists)
            return operand is null ? Result<BlobDimensionPredicate>.Success(new(name, operation, null)) : Invalid();
        var value = BlobDimensionValue.Create(operand);
        if (value.IsFailure || value.Value.ValueType == BlobDimensionValueType.Null) return Invalid();
        if (operation is not (BlobDimensionOperator.Equal or BlobDimensionOperator.NotEqual)
            && value.Value.ValueType is not (BlobDimensionValueType.Integer or BlobDimensionValueType.Decimal or BlobDimensionValueType.DateTime)) return Invalid();
        return Result<BlobDimensionPredicate>.Success(new(name, operation, value.Value));
    }

    private static Result<BlobDimensionPredicate> Invalid() => Result<BlobDimensionPredicate>.Failure(new BlobStoreValidationError("Dimension predicate name, operator, or operand is invalid."));
}
