// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using BridgingIT.DevKit.Common;
using DevKit.Domain;

public class AdAccountShouldBePartOfDomain(string value) : DomainRuleBase
{
    public override string Message => "AD Account should be part of a domain";

    protected override Result ExecuteRule()
    {
        return Result.SuccessIf(value.Contains('\\'));
    }
}