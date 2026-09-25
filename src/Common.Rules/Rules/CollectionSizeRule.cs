// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Enforces constraints on the size of a collection, ensuring it meets specified criteria.
/// </summary>
public class CollectionSizeRule<T>(IEnumerable<T> collection, int minSize, int maxSize) : RuleBase
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    /// <value>
    /// A string representing the message content.
    /// </value>
    public override string Message =>
        $"Collection size must be between {minSize} and {maxSize} items";

    /// <summary>
    /// Checks whether the collection count is within the configured inclusive bounds.
    /// </summary>
    /// <returns>A successful result when the count is between the minimum and maximum sizes; otherwise, a failure result.</returns>
    public override Result Execute()
    {
        var count = collection?.Count() ?? 0;

        return Result.SuccessIf(count >= minSize && count <= maxSize);
    }
}