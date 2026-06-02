// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Finds a <see cref="CurrentWeather"/> by its associated <see cref="CityId"/>.
/// </summary>
public class CurrentWeatherByCitySpecification(CityId cityId) : Specification<CurrentWeather>
{
    public override Expression<Func<CurrentWeather, bool>> ToExpression()
    {
        return cw => cw.CityId == cityId;
    }
}

/// <summary>
/// Finds a <see cref="WeatherForecast"/> by <see cref="CityId"/> and forecast date.
/// </summary>
public class WeatherForecastByCityAndDateSpecification(CityId cityId, DateOnly forecastDate) : Specification<WeatherForecast>
{
    public override Expression<Func<WeatherForecast, bool>> ToExpression()
    {
        return wf => wf.CityId == cityId && wf.ForecastDate == forecastDate;
    }
}
