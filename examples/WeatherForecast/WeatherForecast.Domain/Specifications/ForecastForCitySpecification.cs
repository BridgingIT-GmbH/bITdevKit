﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain;
using Model;

public class ForecastForCitySpecification(Guid cityId) : Specification<Forecast>
{
    private readonly Guid cityId = cityId;

    public override Expression<Func<Forecast, bool>> ToExpression()
    {
        return e => e.CityId == this.cityId;
    }
}