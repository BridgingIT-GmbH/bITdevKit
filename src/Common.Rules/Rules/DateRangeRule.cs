// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if a specified date is within a given range.
/// </summary>
/// <param name="value">The date value to be validated.</param>
/// <param name="start">The start date of the range.</param>
/// <param name="end">The end date of the range.</param>
/// <param name="inclusive">Indicates whether the range is inclusive of the start and end dates.</param>
public class DateRangeRule(DateTime value, DateTime start, DateTime end, bool inclusive) : RuleBase
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    /// <value>
    /// The message content.
    /// </value>
    public override string Message => $"Date must be {(inclusive ? "between" : "strictly between")} {start} and {end}";

    /// <summary>
    /// Checks whether the date falls within the configured range.
    /// </summary>
    /// <returns>A successful result when the date is in range according to the configured boundary mode; otherwise, a failure result.</returns>
    public override Result Execute() =>
        Result.SuccessIf(value.IsInRange(start, end, inclusive));
}