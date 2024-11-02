// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

public class BusinessHoursRule(DateTime dateTime) : DomainRuleBase
{
    public override bool IsEnabled =>
        dateTime.DayOfWeek != DayOfWeek.Saturday &&
        dateTime.DayOfWeek != DayOfWeek.Sunday;

    protected override Result ExecuteRule()
    {
        if (dateTime.Hour < 9 || dateTime.Hour >= 17)
        {
            return Result.Failure()
                .WithError(new DomainRuleError(
                    nameof(BusinessHoursRule),
                    "Operation can only be performed during business hours (9 AM - 5 PM)"));
        }

        return Result.Success();
    }
}