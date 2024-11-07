// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a rule that validates whether a specified time value is within a given relative range from the current time.
/// </summary>
/// <param name="value">The time value to be validated.</param>
/// <param name="unit">The time unit (Minute, Hour) used to define the range.</param>
/// <param name="amount">The amount of the time unit to define the range.</param>
/// <param name="direction">The direction (Past, Future) from the current time for the validation.</param>
public class TimeRelativeRule(TimeOnly value, TimeUnit unit, int amount, DateTimeDirection direction) : RuleBase
{
    /// <summary>
    /// Gets the descriptive message associated with the rule, indicating the condition that must be met.
    /// </summary>
    public override string Message => $"Time must be within {amount} {unit}(s) {direction.ToString().ToLower()} from now";

    /// <summary>
    /// Executes the validation rule and returns a Result object indicating the success or failure of the rule.
    /// </summary>
    /// <returns>
    /// A Result object containing the outcome of the rule execution.
    /// </returns>
    protected override Result Execute() =>
        Result.SuccessIf(value.IsInRelativeRange(unit, amount, direction));
}