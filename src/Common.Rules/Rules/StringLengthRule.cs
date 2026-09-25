// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Enforces constraints on the length of string values ensuring they meet specified minimum and maximum length criteria.
/// </summary>
public class StringLengthRule(string value, int minLength, int maxLength, string message = null) : RuleBase
{
    private readonly string message = message ?? $"Text length must be between {minLength} and {maxLength} characters";

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    /// <value>
    /// The message is represented as a string, and it contains the content that will be transmitted or displayed.
    /// </value>
    public override string Message => this.message;

    /// <summary>
    /// Checks whether the string length is within the configured inclusive bounds.
    /// </summary>
    /// <returns>A successful result when the string length is within bounds; otherwise, a failure result.</returns>
    public override Result Execute() =>
        Result.SuccessIf(value?.Length >= minLength && value?.Length <= maxLength);
}