// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure.OpenMeteo;

/// <summary>
/// Represents weather data returned from the Open-Meteo Forecast API.
/// </summary>
public class WeatherData
{
    /// <summary>Gets or sets the current weather conditions.</summary>
    public CurrentWeatherData Current { get; set; }

    /// <summary>Gets or sets the daily forecast data.</summary>
    public List<DailyForecastData> Daily { get; set; } = [];

    /// <summary>Gets or sets the hourly forecast data.</summary>
    public List<HourlyForecastData> Hourly { get; set; } = [];
}

/// <summary>
/// Represents current weather data from the Open-Meteo API.
/// </summary>
public class CurrentWeatherData
{
    /// <summary>Gets or sets the temperature in degrees Celsius.</summary>
    public decimal Temperature { get; set; }

    /// <summary>Gets or sets the apparent (feels-like) temperature in degrees Celsius.</summary>
    public decimal ApparentTemperature { get; set; }

    /// <summary>Gets or sets the WMO weather condition code.</summary>
    public int WeatherCode { get; set; }

    /// <summary>Gets or sets the wind speed in km/h.</summary>
    public decimal WindSpeed { get; set; }

    /// <summary>Gets or sets the wind direction in degrees.</summary>
    public int WindDirection { get; set; }

    /// <summary>Gets or sets the wind gusts in km/h.</summary>
    public decimal WindGusts { get; set; }

    /// <summary>Gets or sets the relative humidity percentage.</summary>
    public int Humidity { get; set; }

    /// <summary>Gets or sets the precipitation amount in mm.</summary>
    public decimal Precipitation { get; set; }

    /// <summary>Gets or sets the cloud cover percentage.</summary>
    public int CloudCover { get; set; }

    /// <summary>Gets or sets the atmospheric pressure in hPa.</summary>
    public decimal Pressure { get; set; }
}

/// <summary>
/// Represents daily forecast data from the Open-Meteo API.
/// </summary>
public class DailyForecastData
{
    /// <summary>Gets or sets the forecast date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the daytime WMO weather condition code.</summary>
    public int WeatherCode { get; set; }

    /// <summary>Gets or sets the maximum temperature in degrees Celsius.</summary>
    public decimal TemperatureMax { get; set; }

    /// <summary>Gets or sets the minimum temperature in degrees Celsius.</summary>
    public decimal TemperatureMin { get; set; }

    /// <summary>Gets or sets the maximum apparent temperature in degrees Celsius.</summary>
    public decimal ApparentTemperatureMax { get; set; }

    /// <summary>Gets or sets the minimum apparent temperature in degrees Celsius.</summary>
    public decimal ApparentTemperatureMin { get; set; }

    /// <summary>Gets or sets the total precipitation sum in mm.</summary>
    public decimal PrecipitationSum { get; set; }

    /// <summary>Gets or sets the maximum precipitation probability percentage.</summary>
    public int PrecipitationProbabilityMax { get; set; }

    /// <summary>Gets or sets the maximum wind speed in km/h.</summary>
    public decimal WindSpeedMax { get; set; }

    /// <summary>Gets or sets the maximum wind gusts in km/h.</summary>
    public decimal WindGustsMax { get; set; }

    /// <summary>Gets or sets the dominant wind direction in degrees.</summary>
    public int DominantWindDirection { get; set; }

    /// <summary>Gets or sets the maximum UV index.</summary>
    public decimal UvIndexMax { get; set; }

    /// <summary>Gets or sets the sunshine duration in seconds.</summary>
    public int SunshineDurationSeconds { get; set; }

    /// <summary>Gets or sets the daylight duration in seconds.</summary>
    public int DaylightDurationSeconds { get; set; }

    /// <summary>Gets or sets the sunrise time.</summary>
    public DateTime Sunrise { get; set; }

    /// <summary>Gets or sets the sunset time.</summary>
    public DateTime Sunset { get; set; }
}

/// <summary>
/// Represents hourly forecast data from the Open-Meteo API.
/// </summary>
public class HourlyForecastData
{
    /// <summary>Gets or sets the forecast time.</summary>
    public DateTime Time { get; set; }

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
