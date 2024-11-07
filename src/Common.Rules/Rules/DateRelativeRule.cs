// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if a given date is within a specified range relative to the current date.
/// </summary>
public class DateRelativeRule(DateTime value, DateUnit unit, int amount, DateTimeDirection direction) : RuleBase
{
    /// <summary>
    /// Gets the message describing the result of the rule evaluation.
    /// </summary>
    /// <remarks>
    /// This message provides information about why the rule was not satisfied.
    /// The default implementation returns "Rule not satisfied". Derived classes
    /// can override this property to provide specific messages relevant to the rule.
    /// </remarks>
    public override string Message => $"Date must be within {amount} {unit}(s) {direction.ToString().ToLower()} from now";

    /// <summary>
    /// Executes the rule, determining if it passes or fails.
    /// </summary>
    /// <returns>A result indicating the success or failure of the rule.</returns>
    protected override Result Execute() =>
        Result.SuccessIf(value.IsInRelativeRange(unit, amount, direction));
}