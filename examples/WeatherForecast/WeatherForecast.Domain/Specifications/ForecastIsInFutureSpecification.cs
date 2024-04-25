// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Specifications;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

public class ForecastIsInFutureSpecification : Specification<Forecast>
{
    private readonly DateTime timestamp;

    public ForecastIsInFutureSpecification()
    {
        this.timestamp = DateTime.Today.AddDays(-1);
    }

    public override Expression<Func<Forecast, bool>> ToExpression()
    {
        return f => f.Timestamp >= this.timestamp;
    }
}
