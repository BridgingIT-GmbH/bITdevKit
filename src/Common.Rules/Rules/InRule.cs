// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a single validation rule that can be applied to input values.
/// </summary>
public class InRule<T>(T value, IEnumerable<T> allowedValues)
    : RuleBase
{
    /// <summary>
    /// Gets or sets the message text.
    /// </summary>
    /// <value>
    /// The message text content.
    /// </value>
    public override string Message => $"Value must be one of: {string.Join(", ", allowedValues)}";

    /// <summary>
    /// Executes the specified rule with provided parameters.
    /// </summary>
    /// <param name="ruleName">The name of the rule to execute.</param>
    /// <param name="parameters">An array of objects representing the parameters for the rule.</param>
    /// <return>Returns the result of rule execution.</return>
    protected override Result Execute() => Result.SuccessIf(allowedValues.Contains(value));
}