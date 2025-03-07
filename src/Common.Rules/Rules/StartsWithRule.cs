// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Validates whether a specified string value starts with a given prefix using the specified string comparison option.
/// </summary>
public class StartsWithRule(string value, string prefix, StringComparison comparison)
    : RuleBase
{
    /// <summary>
    /// Gets the message associated with the rule, providing detailed information about the rule's constraint.
    /// </summary>
    public override string Message => $"Value must start with '{prefix}'";

    /// <summary>
    /// Executes the rule and returns the result.
    /// </summary>
    /// <returns>Success if the rule is satisfied; otherwise, an error result.</returns>
    public override Result Execute() =>
        Result.SuccessIf(!string.IsNullOrEmpty(value) &&
            value.StartsWith(prefix, comparison));
}