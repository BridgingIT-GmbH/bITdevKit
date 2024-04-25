// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System.Collections.Generic;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

public class CityQueryResponse // this could also be an application viewmodel
{
    private CityQueryResponse(City city, IEnumerable<Forecast> forecasts)
    {
        this.City = city;
        this.Forecasts = forecasts;
    }

    public City City { get; }

    public IEnumerable<Forecast> Forecasts { get; }

    public static CityQueryResponse Create(City city, IEnumerable<Forecast> forecasts = null)
    {
        return new CityQueryResponse(city, forecasts);
    }
}
