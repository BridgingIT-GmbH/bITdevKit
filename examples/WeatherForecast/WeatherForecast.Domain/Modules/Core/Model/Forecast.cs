// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

using System;
using System.Diagnostics;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

[DebuggerDisplay("Id={Id}, CityId={CityId}, Description={Description}")]
public class Forecast : AggregateRoot<Guid>
{
    private Forecast()
    {
    }

    public Guid CityId { get; private set; }

    public DateTimeOffset Timestamp { get; private set; }

    public string Description { get; private set; }

    public double? TemperatureMin { get; private set; }

    public double? TemperatureMax { get; private set; }

    public double? WindSpeed { get; private set; }

    public Guid TypeId { get; set; }

    public ForecastType Type { get; set; }

    public static Forecast Create(
        Guid cityId,
        DateTimeOffset timestamp,
        string description,
        double temperatureMin,
        double temperatureMax,
        double windSpeed)
    {
        return new Forecast
        {
            Id = GuidGenerator.Create($"{cityId}-{timestamp.ToUnixTimeSeconds()}"), // create repeatable id auf basis cityid-timestamp>guid (=upsert friendly)
            CityId = cityId,
            Timestamp = timestamp,
            Description = description,
            TemperatureMax = temperatureMax,
            TemperatureMin = temperatureMin,
            WindSpeed = windSpeed
        };
    }
}
