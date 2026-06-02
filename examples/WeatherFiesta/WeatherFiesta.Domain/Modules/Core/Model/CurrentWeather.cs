// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Represents the current weather conditions for a city at a point in time.
/// </summary>
[DebuggerDisplay("Id={Id}, CityId={CityId}, Temperature={Temperature}°C")]
[TypedEntityId<Guid>]
public class CurrentWeather : ActiveEntity<CurrentWeather, CurrentWeatherId>
{
    /// <summary>Gets or sets the city identifier.</summary>
    public CityId CityId { get; set; }

    /// <summary>Gets or sets the temperature in degrees Celsius.</summary>
    public decimal Temperature { get; set; }

    /// <summary>Gets or sets the apparent (feels-like) temperature in degrees Celsius.</summary>
    public decimal ApparentTemperature { get; set; }

    /// <summary>Gets or sets the relative humidity percentage.</summary>
    public int Humidity { get; set; }

    /// <summary>Gets or sets the WMO weather condition code.</summary>
    public int WeatherCode { get; set; }

    /// <summary>Gets or sets the wind speed in km/h.</summary>
    public decimal WindSpeed { get; set; }

    /// <summary>Gets or sets the wind direction in degrees.</summary>
    public int WindDirection { get; set; }

    /// <summary>Gets or sets the wind gusts in km/h.</summary>
    public decimal WindGusts { get; set; }

    /// <summary>Gets or sets the precipitation amount in mm.</summary>
    public decimal Precipitation { get; set; }

    /// <summary>Gets or sets the cloud cover percentage.</summary>
    public int CloudCover { get; set; }

    /// <summary>Gets or sets the atmospheric pressure in hPa.</summary>
    public decimal Pressure { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the weather data was retrieved.</summary>
    public DateTime RetrievedAt { get; set; }

    private CurrentWeather() { } // EF Core

    /// <summary>
    /// Creates a new <see cref="CurrentWeather"/> instance for the specified city.
    /// </summary>
    /// <param name="cityId">The city identifier.</param>
    /// <returns>A new <see cref="CurrentWeather"/> instance.</returns>
    public static CurrentWeather Create(CityId cityId)
    {
        return new CurrentWeather
        {
            CityId = cityId
        };
    }

    /// <summary>
    /// Checks if the weather data is stale based on the given threshold.
    /// </summary>
    /// <param name="staleThreshold">The time span after which data is considered stale.</param>
    /// <returns><c>true</c> if the data is older than the threshold; otherwise <c>false</c>.</returns>
    public bool IsStale(TimeSpan staleThreshold)
    {
        return DateTime.UtcNow - this.RetrievedAt > staleThreshold;
    }
}
