// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Represents an hourly weather forecast entry within a daily forecast.
/// </summary>
public class HourlyForecast
{
    /// <summary>Gets or sets the hour of the forecast (0-23).</summary>
    public int Hour { get; set; }

    /// <summary>Gets or sets the temperature in degrees Celsius.</summary>
    public decimal Temperature { get; set; }

    /// <summary>Gets or sets the relative humidity percentage.</summary>
    public int RelativeHumidity { get; set; }

    /// <summary>Gets or sets the apparent (feels-like) temperature in degrees Celsius.</summary>
    public decimal ApparentTemperature { get; set; }

    /// <summary>Gets or sets the precipitation probability percentage.</summary>
    public int PrecipitationProbability { get; set; }

    /// <summary>Gets or sets the precipitation amount in mm.</summary>
    public decimal Precipitation { get; set; }

    /// <summary>Gets or sets the WMO weather condition code.</summary>
    public int WeatherCode { get; set; }

    /// <summary>Gets or sets the wind speed in km/h.</summary>
    public decimal WindSpeed { get; set; }

    /// <summary>Gets or sets the wind direction in degrees.</summary>
    public int WindDirection { get; set; }

    /// <summary>Gets or sets the wind gusts in km/h.</summary>
    public decimal WindGusts { get; set; }

    /// <summary>Gets or sets the cloud cover percentage.</summary>
    public int CloudCover { get; set; }

    /// <summary>Gets or sets the visibility in meters.</summary>
    public decimal Visibility { get; set; }

    /// <summary>Gets or sets a value indicating whether it is daytime.</summary>
    public bool IsDay { get; set; }
}
