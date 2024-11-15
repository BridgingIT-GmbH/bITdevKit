// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if a given string value ends with a specified suffix.
/// </summary>
public class EndsWithRule(string value, string suffix, StringComparison comparison)
    : RuleBase
{
    /// <summary>
    /// Gets the message associated with the rule, indicating the specific requirement or error condition.
    /// </summary>
    public override string Message => $"Value must end with '{suffix}'";

    /// <summary>
    /// Executes a rule and returns a result indicating success or failure.
    /// </summary>
    /// <returns>A Result object indicating the outcome of the rule execution.</returns>
    protected override Result Execute() =>
        Result.SuccessIf(!string.IsNullOrEmpty(value) &&
            value.EndsWith(suffix, comparison));
}