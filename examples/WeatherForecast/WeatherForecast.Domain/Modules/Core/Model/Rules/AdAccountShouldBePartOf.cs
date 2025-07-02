// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using BridgingIT.DevKit.Common;

public class AdAccountShouldBePartOf(string value) : RuleBase
{
    public override string Message => "AD Account should be part of a domain";

    public override Result Execute()
    {
        return Result.SuccessIf(value.Contains('\\'));
    }
}