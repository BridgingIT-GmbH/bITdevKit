// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Defines the bounded document dimension predicate operators.</summary>
/// <example><code>var predicate = DocumentDimensionPredicate.Create("Priority", DocumentDimensionOperator.Equal, 3);</code></example>
public enum DocumentDimensionOperator
{
    /// <summary>Matches a present, non-null value of the same kind equal to the operand.</summary>
    Equal,

    /// <summary>Matches a present, non-null value of the same kind different from the operand.</summary>
    NotEqual,

    /// <summary>Matches an ordered value greater than the operand.</summary>
    GreaterThan,

    /// <summary>Matches an ordered value greater than or equal to the operand.</summary>
    GreaterThanOrEqual,

    /// <summary>Matches an ordered value less than the operand.</summary>
    LessThan,

    /// <summary>Matches an ordered value less than or equal to the operand.</summary>
    LessThanOrEqual,

    /// <summary>Matches any present entry, including explicit null.</summary>
    Exists,

    /// <summary>Matches an absent entry.</summary>
    Missing,

    /// <summary>Matches a present explicit null.</summary>
    IsNull,

    /// <summary>Matches a present value other than null.</summary>
    IsNotNull
}
