// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Defines the portable document Dimensions limits.</summary>
/// <example><code>var limit = DocumentDimensionLimits.MaximumCount;</code></example>
public static class DocumentDimensionLimits
{
    /// <summary>Maximum entries, including explicit nulls, on one owner.</summary>
    public const int MaximumCount = 64;

    /// <summary>Maximum ASCII characters in an identifier.</summary>
    public const int MaximumNameLength = 128;

    /// <summary>Maximum strict UTF-8 bytes in a string value.</summary>
    public const int MaximumStringBytes = 256;

    /// <summary>Maximum predicates in a filter.</summary>
    public const int MaximumPredicates = 16;

    /// <summary>Maximum groups in a filter.</summary>
    public const int MaximumGroups = 16;

    /// <summary>Maximum nested group levels.</summary>
    public const int MaximumGroupDepth = 4;

    /// <summary>Maximum absolute decimal magnitude.</summary>
    public const decimal MaximumDecimal = 999999999999.999999m;
}
