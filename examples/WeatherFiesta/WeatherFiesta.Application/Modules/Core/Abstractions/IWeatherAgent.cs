// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;

/// <summary>
/// Service agent abstraction for weather data operations.
/// Decouples business logic from specific weather data providers.
/// </summary>
public interface IWeatherAgent
{
    /// <summary>
    /// Checks whether the weather agent can retrieve and map data from its backing provider.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result describing the provider connectivity and mapping check outcome.</returns>
    /// <example>
    /// <code>
    /// var result = await weatherAgent.CheckHealthAsync(cancellationToken);
    /// </code>
    /// </example>
    Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingests current weather and forecast data for the specified city.
    /// </summary>
    /// <param name="cityId">The city ID to ingest weather data for.</param>
    /// <param name="latitude">The latitude of the city.</param>
    /// <param name="longitude">The longitude of the city.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the ingestion result with updated weather data.</returns>
    Task<Result<WeatherIngestionResult>> IngestWeatherAsync(
        string cityId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a weather ingestion operation containing current weather and forecasts.
/// </summary>
public class WeatherIngestionResult
{
    /// <summary>Gets or sets the current weather data.</summary>
    public CurrentWeatherData CurrentWeather { get; set; }

    /// <summary>Gets or sets the forecast data.</summary>
    public List<ForecastData> Forecasts { get; set; } = [];
}

/// <summary>
/// Current weather data from the weather agent.
/// </summary>
public class CurrentWeatherData
{
    /// <summary>Gets or sets the temperature in Celsius.</summary>
    public double TemperatureCelsius { get; set; }

    /// <summary>Gets or sets the apparent (feels-like) temperature in Celsius.</summary>
    public double ApparentTemperatureCelsius { get; set; }

    /// <summary>Gets or sets the relative humidity percentage.</summary>
    public double RelativeHumidity { get; set; }

    /// <summary>Gets or sets the wind speed in km/h.</summary>
    public double WindSpeedKmh { get; set; }

    /// <summary>Gets or sets the wind gusts in km/h.</summary>
    public double WindGustsKmh { get; set; }

    /// <summary>Gets or sets the wind direction in degrees.</summary>
    public double WindDirectionDegrees { get; set; }

    /// <summary>Gets or sets the weather condition code (WMO code).</summary>
    public int WeatherCode { get; set; }

    /// <summary>Gets or sets the atmospheric pressure in hPa.</summary>
    public double PressureHpa { get; set; }

    /// <summary>Gets or sets the cloud cover percentage.</summary>
    public double CloudCoverPercent { get; set; }

    /// <summary>Gets or sets the precipitation amount in mm.</summary>
    public double PrecipitationMm { get; set; }

    /// <summary>Gets or sets the timestamp when the data was retrieved.</summary>
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Forecast data for a single day from the weather agent.
/// </summary>
public class ForecastData
{
    /// <summary>Gets or sets the forecast date.</summary>
    public DateTime ForecastDate { get; set; }

    /// <summary>Gets or sets the maximum temperature in Celsius.</summary>
    public double TemperatureMaxCelsius { get; set; }

    /// <summary>Gets or sets the minimum temperature in Celsius.</summary>
    public double TemperatureMinCelsius { get; set; }

    /// <summary>Gets or sets the maximum apparent temperature in Celsius.</summary>
    public double ApparentTemperatureMaxCelsius { get; set; }

    /// <summary>Gets or sets the minimum apparent temperature in Celsius.</summary>
    public double ApparentTemperatureMinCelsius { get; set; }

    /// <summary>Gets or sets the weather condition code (WMO code).</summary>
    public int WeatherCode { get; set; }

    /// <summary>Gets or sets the total precipitation sum in mm.</summary>
    public double PrecipitationSumMm { get; set; }

    /// <summary>Gets or sets the precipitation probability percentage.</summary>
    public double PrecipitationProbability { get; set; }

    /// <summary>Gets or sets the maximum wind speed in km/h.</summary>
    public double WindSpeedMaxKmh { get; set; }

    /// <summary>Gets or sets the maximum wind gusts in km/h.</summary>
    public double WindGustsMaxKmh { get; set; }

    /// <summary>Gets or sets the dominant wind direction in degrees.</summary>
    public int DominantWindDirectionDegrees { get; set; }

    /// <summary>Gets or sets the sunshine duration in seconds.</summary>
    public int SunshineDurationSeconds { get; set; }

    /// <summary>Gets or sets the daylight duration in seconds.</summary>
    public int DaylightDurationSeconds { get; set; }

    /// <summary>Gets or sets the sunrise time.</summary>
    public DateTime? Sunrise { get; set; }

    /// <summary>Gets or sets the sunset time.</summary>
    public DateTime? Sunset { get; set; }

    /// <summary>Gets or sets the UV index.</summary>
    public double? UvIndex { get; set; }

    /// <summary>Gets or sets the hourly forecast data.</summary>
    public List<HourlyForecastData> HourlyForecasts { get; set; } = [];
}

/// <summary>
/// Hourly forecast data from the weather agent.
/// </summary>
public class HourlyForecastData
{
    /// <summary>Gets or sets the hour.</summary>
    public DateTime Hour { get; set; }

    /// <summary>Gets or sets the temperature in Celsius.</summary>
    public double TemperatureCelsius { get; set; }

    /// <summary>Gets or sets the weather condition code (WMO code).</summary>
    public int WeatherCode { get; set; }

    /// <summary>Gets or sets the precipitation probability percentage.</summary>
    public double PrecipitationProbability { get; set; }

    /// <summary>Gets or sets the wind speed in km/h.</summary>
    public double WindSpeedKmh { get; set; }

    /// <summary>Gets or sets the relative humidity percentage.</summary>
    public double RelativeHumidity { get; set; }

    /// <summary>Gets or sets the apparent (feels-like) temperature in Celsius.</summary>
    public double ApparentTemperatureCelsius { get; set; }

    /// <summary>Gets or sets the precipitation amount in mm.</summary>
    public double PrecipitationMm { get; set; }

    /// <summary>Gets or sets the wind direction in degrees.</summary>
    public int WindDirectionDegrees { get; set; }

    /// <summary>Gets or sets the wind gusts in km/h.</summary>
    public double WindGustsKmh { get; set; }

    /// <summary>Gets or sets the cloud cover percentage.</summary>
    public int CloudCoverPercent { get; set; }

    /// <summary>Gets or sets the visibility in meters.</summary>
    public double VisibilityMeters { get; set; }

    /// <summary>Gets or sets a value indicating whether it is daytime.</summary>
    public bool IsDay { get; set; }

    /// <summary>Gets or sets the UV index.</summary>
    public double? UvIndex { get; set; }
}
