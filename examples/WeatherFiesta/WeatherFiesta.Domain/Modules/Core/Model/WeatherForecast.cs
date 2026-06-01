// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Model;

/// <summary>
/// Represents a daily weather forecast for a city, including hourly breakdowns.
/// </summary>
[DebuggerDisplay("Id={Id}, CityId={CityId}, Date={ForecastDate}")]
[TypedEntityId<Guid>]
public class WeatherForecast : ActiveEntity<WeatherForecast, WeatherForecastId>, IConcurrency
{
    /// <summary>Gets or sets the city identifier.</summary>
    public CityId CityId { get; set; }

    /// <summary>Gets or sets the forecast date.</summary>
    public DateOnly ForecastDate { get; set; }

    /// <summary>Gets or sets the daytime WMO weather condition code.</summary>
    public int DayWeatherCode { get; set; }

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

    /// <summary>Gets or sets the hourly forecast breakdown for this day.</summary>
    public ICollection<HourlyForecast> HourlyForecasts { get; set; } = [];

    /// <summary>Gets or sets the UTC timestamp when the forecast data was retrieved.</summary>
    public DateTime RetrievedAt { get; set; }

    /// <summary>Gets or sets the concurrency version for optimistic concurrency control.</summary>
    public Guid ConcurrencyVersion { get; set; }

    private WeatherForecast() { } // EF Core

    /// <summary>
    /// Creates a new <see cref="WeatherForecast"/> instance for the specified city.
    /// </summary>
    /// <param name="cityId">The city identifier.</param>
    /// <returns>A new <see cref="WeatherForecast"/> instance.</returns>
    public static WeatherForecast Create(CityId cityId)
    {
        return new WeatherForecast
        {
            CityId = cityId
        };
    }

    /// <summary>
    /// Checks if the forecast data is stale based on the given threshold.
    /// </summary>
    /// <param name="staleThreshold">The time span after which data is considered stale.</param>
    /// <returns><c>true</c> if the data is older than the threshold; otherwise <c>false</c>.</returns>
    public bool IsStale(TimeSpan staleThreshold)
    {
        return DateTime.UtcNow - this.RetrievedAt > staleThreshold;
    }
}
