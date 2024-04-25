// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System.Linq;
using BridgingIT.DevKit.Common;

public class CityModelMapper : IMapper<CityQueryResponse, CityModel>
{
    public void Map(CityQueryResponse source, CityModel target)
    {
        if (source.City is not null)
        {
            target.Id = source.City.Id;
            target.Name = source.City.Name;
            target.Country = source.City.Country;
            target.Longitude = source.City.Location is not null ? source.City.Location.Longitude : 0;
            target.Latitude = source.City.Location is not null ? source.City.Location.Latitude : 0;
            target.Forecasts = source.Forecasts.SafeNull().Select(f =>
                new ForecastModel
                {
                    Description = f.Description,
                    TemperatureMin = f.TemperatureMin,
                    TemperatureMax = f.TemperatureMax,
                    WindSpeed = f.WindSpeed,
                    Timestamp = f.Timestamp
                });
        }
    }
}
