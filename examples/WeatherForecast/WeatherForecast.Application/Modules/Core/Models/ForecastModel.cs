// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System;

public class ForecastModel
{
    public Guid Id { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string Description { get; set; }

    public double? TemperatureMin { get; set; }

    public double? TemperatureMax { get; set; }

    public double? WindSpeed { get; set; }

    public string Type { get; internal set; }
}
