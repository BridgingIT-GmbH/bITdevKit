// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;

/// <summary>Validates names and serializes independent typed blob Dimensions snapshots.</summary>
/// <example><code>var snapshot = BlobDimensionValues.Serialize(dimensions);</code></example>
public static partial class BlobDimensionValues
{
    /// <summary>Tests the bounded ASCII identifier grammar without trimming.</summary>
    /// <example><code>var valid = BlobDimensionValues.IsValidName("FiscalYear");</code></example>
    public static bool IsValidName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > BlobDimensionLimits.MaximumNameLength
            || !(char.IsAsciiLetter(name[0]) || name[0] == '_')) return false;
        return name.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or ':' or '-');
    }

    /// <summary>Returns the stable ASCII uppercase identity of a validated name.</summary>
    /// <exception cref="ArgumentException">The name is invalid.</exception>
    /// <example><code>var identity = BlobDimensionValues.CanonicalizeName("fiscalYear");</code></example>
    public static string CanonicalizeName(string name) => IsValidName(name)
        ? name.ToUpperInvariant() : throw new ArgumentException("Dimension name is invalid.", nameof(name));

    /// <summary>Validates an explicitly supplied immutable collection.</summary>
    /// <example><code>var valid = BlobDimensionValues.Validate(BlobDimensions.Empty);</code></example>
    public static Result Validate(BlobDimensions dimensions) => dimensions is null
        ? Result.Failure(new ValidationError("Dimensions input is required.")) : Result.Success();

}
