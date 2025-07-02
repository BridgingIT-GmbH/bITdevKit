// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using BridgingIT.DevKit.Common;

public class DeleteMustBeProvidedReasonRule(string reason) : RuleBase
{
    public override string Message => "Reason of deleting a must be provided";

    public override Result Execute()
    {
        return Result.SuccessIf(!string.IsNullOrEmpty(reason));
    }
}