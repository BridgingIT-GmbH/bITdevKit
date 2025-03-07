// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Enforces constraints on the length of string values ensuring they meet specified minimum and maximum length criteria.
/// </summary>
public class StringLengthRule(string value, int minLength, int maxLength) : RuleBase
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    /// <value>
    /// The message is represented as a string, and it contains the content that will be transmitted or displayed.
    /// </value>
    public override string Message =>
        $"Text length must be between {minLength} and {maxLength} characters";

    /// <summary>
    /// Executes the provided rule and returns a boolean indicating success or failure.
    /// </summary>
    /// <param name="rule">The rule to be executed.</param>
    /// <param name="context">The context in which the rule should be executed.</param>
    /// <return>A boolean indicating whether the rule executed successfully or not.</return>
    public override Result Execute() =>
        Result.SuccessIf(value?.Length >= minLength && value?.Length <= maxLength);
}