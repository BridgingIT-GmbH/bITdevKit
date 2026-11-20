// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>An immutable validated predicate over one logical dimension name.</summary>
/// <example><code>var predicate = DocumentDimensionPredicate.Create("FiscalYear", DocumentDimensionOperator.Equal, 2026);</code></example>
public sealed class DocumentDimensionPredicate
{
    private DocumentDimensionPredicate(string name, DocumentDimensionOperator operation, DocumentDimensionValue operand)
    {
        this.Name = name;
        this.Operator = operation;
        this.Operand = operand;
    }

    /// <summary>Gets the logical name.</summary>
    public string Name { get; }

    /// <summary>Gets the operator.</summary>
    public DocumentDimensionOperator Operator { get; }

    /// <summary>Gets the typed operand, or null for state predicates.</summary>
    public DocumentDimensionValue Operand { get; }

    /// <summary>Creates a predicate; state operators omit an operand and comparisons require non-null values.</summary>
    /// <example><code>var nullCheck = DocumentDimensionPredicate.Create("Approved", DocumentDimensionOperator.IsNull);</code></example>
    public static Result<DocumentDimensionPredicate> Create(string name, DocumentDimensionOperator operation, object operand = null)
    {
        if (!DocumentDimensionValues.IsValidName(name) || !Enum.IsDefined(operation)) return Invalid();
        if (operation >= DocumentDimensionOperator.Exists)
            return operand is null ? Result<DocumentDimensionPredicate>.Success(new(name, operation, null)) : Invalid();
        var value = DocumentDimensionValue.Create(operand);
        if (value.IsFailure || value.Value.ValueType == DocumentDimensionValueType.Null) return Invalid();
        if (operation is not (DocumentDimensionOperator.Equal or DocumentDimensionOperator.NotEqual)
            && value.Value.ValueType is not (DocumentDimensionValueType.Integer or DocumentDimensionValueType.Decimal or DocumentDimensionValueType.DateTime)) return Invalid();
        return Result<DocumentDimensionPredicate>.Success(new(name, operation, value.Value));
    }

    private static Result<DocumentDimensionPredicate> Invalid() => Result<DocumentDimensionPredicate>.Failure(new DocumentStoreInvalidQueryError("Dimension predicate name, operator, or operand is invalid."));
}
