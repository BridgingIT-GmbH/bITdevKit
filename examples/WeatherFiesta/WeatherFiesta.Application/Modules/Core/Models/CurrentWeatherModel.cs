// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;

/// <summary>
/// DTO representing current weather conditions for a city.
/// </summary>
public class CurrentWeatherModel
{
    /// <summary>Gets or sets the weather record identifier.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the city identifier.</summary>
    public string CityId { get; set; }

    /// <summary>Gets or sets the temperature in degrees Celsius.</summary>
    public decimal Temperature { get; set; }

    /// <summary>Gets or sets the apparent (feels-like) temperature.</summary>
    public decimal ApparentTemperature { get; set; }

    /// <summary>Gets or sets the relative humidity percentage.</summary>
    public int Humidity { get; set; }

    /// <summary>Gets or sets the WMO weather condition code.</summary>
    public int WeatherCode { get; set; }

    /// <summary>Gets or sets the human-readable weather description.</summary>
    public string WeatherDescription { get; set; }

    /// <summary>Gets or sets the weather icon identifier.</summary>
    public string WeatherIcon { get; set; }

    /// <summary>Gets or sets the wind speed in km/h.</summary>
    public decimal WindSpeed { get; set; }

    /// <summary>Gets or sets the wind direction in degrees.</summary>
    public int WindDirection { get; set; }

    /// <summary>Gets or sets the wind gusts in km/h.</summary>
    public decimal WindGusts { get; set; }

    /// <summary>Gets or sets the precipitation in mm.</summary>
    public decimal Precipitation { get; set; }

    /// <summary>Gets or sets the cloud cover percentage.</summary>
    public int CloudCover { get; set; }

    /// <summary>Gets or sets the atmospheric pressure in hPa.</summary>
    public decimal Pressure { get; set; }

    /// <summary>Gets or sets when the weather data was retrieved.</summary>
    public DateTime RetrievedAt { get; set; }

    /// <summary>Gets or sets a value indicating whether the data may be stale.</summary>
    public bool StaleDataWarning { get; set; }

    /// <summary>Gets or sets the stale data warning message.</summary>
    public string StaleDataWarningMessage { get; set; }

}
