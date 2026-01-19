// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class BusinessHoursRule(DateTime dateTime, string message = null) : RuleBase
{
    private readonly string message = message ?? "Datetime should be during business hours (MO-FR: 9 AM - 5 PM)";

    public override string Message => this.message;

    public override Result Execute()
    {
        return Result.SuccessIf(
            dateTime.DayOfWeek != DayOfWeek.Saturday &&
            dateTime.DayOfWeek != DayOfWeek.Sunday &&
            dateTime.Hour is < 9 or >= 17);
    }
}