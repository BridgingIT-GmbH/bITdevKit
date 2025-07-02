// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule for ensuring that a value adheres to a predefined set of enumerated values.
/// </summary>
public class EnumValuesRule<TEnum>(TEnum value, IEnumerable<TEnum> allowedValues)
    : RuleBase
    where TEnum : struct, Enum
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    /// <value>
    /// A string representing the body of the message.
    /// </value>
    public override string Message =>
        $"Value must be one of: {string.Join(", ", allowedValues)}";

    /// <summary>
    /// Executes the specified rule.
    /// </summary>
    /// <param name="rule">The rule to be executed.</param>
    /// <param name="context">The context in which the rule is executed.</param>
    /// <returns>The result of the rule execution.</returns>
    public override Result Execute() =>
        Result.SuccessIf(allowedValues.Contains(value));
}