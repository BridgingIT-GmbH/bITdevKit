// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

public class CityModel
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string Country { get; set; }

    public double Longitude { get; set; }

    public double Latitude { get; set; }

    public IEnumerable<ForecastModel> Forecasts { get; set; }
}