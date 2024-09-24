// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using Domain.Model;

public class ForecastQueryResponse // this could also be an application viewmodel
{
    private ForecastQueryResponse(Forecast forecast)
    {
        this.Forecast = forecast;
    }

    public Forecast Forecast { get; }

    public static ForecastQueryResponse Create(Forecast forecast = null)
    {
        return new ForecastQueryResponse(forecast);
    }
}