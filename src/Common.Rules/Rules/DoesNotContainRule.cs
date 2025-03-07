// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a rule that validates whether a string does not contain a specified substring.
/// </summary>
public class DoesNotContainRule(string value, string substring, StringComparison comparison)
    : RuleBase
{
    /// <summary>
    /// Provides a descriptive message that explains why the rule failed.
    /// This message is intended to be user-friendly and specific to the particular rule implementation.
    /// </summary>
    public override string Message => $"Value must not contain '{substring}'";

    /// <summary>
    /// Executes a predefined validation rule.
    /// </summary>
    /// <returns>
    /// A Result object indicating the success or failure of the rule execution.
    /// </returns>
    public override Result Execute() =>
        Result.SuccessIf(string.IsNullOrEmpty(value) ||
            !value.Contains(substring, comparison));
}