﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using BridgingIT.DevKit.Common;
using DevKit.Domain;

public class DeleteCannotBeDoneTwiceRule(bool isDeleted) : DomainRuleBase
{
    public override string Message => "Deleting can only be done once";

    protected override Result ExecuteRule()
    {
        return Result.SuccessIf(!isDeleted);
    }
}