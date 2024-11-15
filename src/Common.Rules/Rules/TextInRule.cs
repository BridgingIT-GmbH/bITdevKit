// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a rule for validating whether a specific text input adheres to predefined criteria.
/// </
public class TextInRule(
    string value,
    IEnumerable<string> allowedValues,
    StringComparison comparison) : RuleBase
{
    /// <summary>
    /// Gets a message that describes the result of the rule.
    /// </summary>
    /// <remarks>
    /// This property is typically used to provide a user-friendly explanation or reason for why a rule passed or failed.
    /// Each specific rule implementation may override this property to provide a more detailed or contextual message.
    /// </remarks>
    public override string Message =>
        $"Text must be one of: {string.Join(", ", allowedValues)}";

    /// <summary>
    /// Executes the rule logic and returns a Result indicating the success or failure of the rule.
    /// </summary>
    /// <returns>
    /// A Result indicating whether the rule was successfully executed.
    /// </returns>
    protected override Result Execute() =>
        Result.SuccessIf(allowedValues.Any(allowed =>
            string.Equals(value, allowed, comparison)));
}