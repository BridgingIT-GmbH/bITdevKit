// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a rule that validates strings based on specific patterns.
/// </summary>
public class PatternRule(string value, string pattern) : RuleBase
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    /// <value>
    /// The message content as a string.
    /// </value>
    public override string Message =>
        $"Value does not match pattern: {pattern}";

    /// <summary>
    /// Executes a specified rule based on the given parameters.
    /// </summary>
    /// <param name="rule">The rule to be executed.</param>
    /// <param name="parameters">Parameters required for the rule execution.</param>
    /// <returns>The result of the rule execution.</returns>
    protected override Result Execute() =>
        Result.SuccessIf(!string.IsNullOrEmpty(value) &&
            System.Text.RegularExpressions.Regex.IsMatch(value, pattern));
}