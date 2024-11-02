﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Common;
using DevKit.Domain;

public class ScheduleShouldBeValidRule(DateTimeOffset startDateTime, DateTimeOffset endDateTime) : DomainRuleBase
{
    public override string Message => "StartDate should be earlier than the EndDate";

    protected override Result ExecuteRule()
    {
        return Result.SuccessIf(startDateTime < endDateTime);
    }
}

public static partial class DinnerRules
{
    public static IDomainRule ScheduleShouldBeValid(DateTimeOffset startDateTime, DateTimeOffset endDateTime)
    {
        return new ScheduleShouldBeValidRule(startDateTime, endDateTime);
    }
}