// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Checks the current implementation's weekday and hour condition for a date and time.</summary>
/// <param name="dateTime">The local date and hour evaluated by the rule.</param>
/// <param name="message">The failure description, or <see langword="null"/> to use the built-in business-hours message.</param>
public class BusinessHoursRule(DateTime dateTime, string message = null) : RuleBase
{
    private readonly string message = message ?? "Datetime should be during business hours (MO-FR: 9 AM - 5 PM)";

    /// <summary>Gets the configured failure message.</summary>
    public override string Message => this.message;

    /// <summary>Returns success for a weekday whose hour is before <c>09:00</c> or at/after <c>17:00</c>; otherwise, returns failure.</summary>
    /// <returns>A result representing the evaluated weekday and hour condition.</returns>
    public override Result Execute()
    {
        return Result.SuccessIf(
            dateTime.DayOfWeek != DayOfWeek.Saturday &&
            dateTime.DayOfWeek != DayOfWeek.Sunday &&
            dateTime.Hour is < 9 or >= 17);
    }
}
