// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines a rule that evaluates whether a given time is within a specified range.
/// </summary>
/// <param name="value">The time value to evaluate.</param>
/// <param name="start">The start of the time range.</param>
/// <param name="end">The end of the time range.</param>
/// <param name="inclusive">Indicates whether the range is inclusive of the start and end times.</param>
public class TimeRangeRule(TimeOnly value, TimeOnly start, TimeOnly end, bool inclusive) : RuleBase
{
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <remarks>
    /// This property holds the textual content that can be used for displaying
    /// messages, notifications, or communications within the application.
    /// The content of this string can vary depending on the context in which it's used.
    /// </remarks>
    public override string Message => $"Time must be {(inclusive ? "between" : "strictly between")} {start} and {end}";

    /// <summary>
    /// Executes a validation rule and returns the result.
    /// </summary>
    /// <returns>
    /// A result object indicating the success or failure of the rule execution.
    /// </returns>
    protected override Result Execute() =>
        Result.SuccessIf(value.IsInRange(start, end, inclusive));
}