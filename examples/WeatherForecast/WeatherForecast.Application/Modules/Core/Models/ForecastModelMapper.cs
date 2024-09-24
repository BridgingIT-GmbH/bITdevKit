// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using Common;

public class ForecastModelMapper : IMapper<ForecastQueryResponse, ForecastModel>
{
    public void Map(ForecastQueryResponse source, ForecastModel target)
    {
        if (source.Forecast is not null)
        {
            target.Id = source.Forecast.Id;
            target.Timestamp = source.Forecast.Timestamp;
            target.TemperatureMax = source.Forecast.TemperatureMax;
            target.TemperatureMin = source.Forecast.TemperatureMin;
            target.Description = source.Forecast.Description;
            target.WindSpeed = source.Forecast.WindSpeed;
            target.Type = source.Forecast.Type?.Name; // included entity
        }
    }
}