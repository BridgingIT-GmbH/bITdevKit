// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Validates bounded expression structure and backend operator support.</summary>
/// <example><code>var validation = DocumentDimensionFilterValidator.Validate(filter, provider.Capabilities.Dimensions);</code></example>
public static class DocumentDimensionFilterValidator
{
    /// <summary>Validates optional predicates before backend access.</summary>
    /// <example><code>var valid = DocumentDimensionFilterValidator.Validate(filter, capabilities);</code></example>
    public static Result Validate(DocumentDimensionFilter filter, DocumentDimensionCapabilities capabilities)
    {
        if (filter is null) return Result.Success();
        if (capabilities?.SupportsQueries != true) return Result.Failure(new DocumentDimensionsNotSupportedError());
        var predicates = 0;
        var groups = 0;
        return Visit(filter, 0);

        Result Visit(DocumentDimensionFilter node, int depth)
        {
            if (node is null) return Invalid();
            if (node.Predicate is { } predicate)
            {
                if (++predicates > DocumentDimensionLimits.MaximumPredicates) return Invalid();
                return capabilities.Supports(predicate.Operand?.ValueType ?? DocumentDimensionValueType.Null, predicate.Operator)
                    ? Result.Success() : Result.Failure(new DocumentStoreQueryNotSupportedError("Dimension operator is unsupported by this provider."));
            }

            if (++groups > DocumentDimensionLimits.MaximumGroups || ++depth > DocumentDimensionLimits.MaximumGroupDepth || node.Children.Count == 0) return Invalid();
            foreach (var child in node.Children)
            {
                var result = Visit(child, depth);
                if (result.IsFailure) return result;
            }

            return Result.Success();
        }
    }

    private static Result Invalid() => Result.Failure(new DocumentStoreInvalidQueryError("Dimension filter is empty, invalid, or exceeds its structural limits."));
}
