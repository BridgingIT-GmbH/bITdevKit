// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Represents a city entity with geographic metadata and current weather data.
/// </summary>
[DebuggerDisplay("Id={Id}, Name={Name}, Country={CountryCode}")]
[TypedEntityId<Guid>]
public class City : ActiveEntity<City, CityId>, IAuditable, IConcurrency
{
    /// <summary>Gets or sets the city name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets or sets the country name.</summary>
    public string Country { get; private set; }

    /// <summary>Gets or sets the ISO 3166-1 alpha-2 country code.</summary>
    public string CountryCode { get; private set; }

    /// <summary>Gets or sets the IANA timezone identifier.</summary>
    public string TimeZone { get; private set; }

    /// <summary>Gets or sets the geographic location (latitude/longitude).</summary>
    public Location Location { get; private set; }

    /// <summary>Gets or sets the elevation in meters above sea level.</summary>
    public decimal? Elevation { get; private set; }

    /// <summary>Gets or sets the external identifier from the geocoding provider (Open-Meteo).</summary>
    public long? ExternalId { get; set; }

    /// <summary>Gets or sets the most recent current weather data for this city.</summary>
    public CurrentWeather CurrentWeather { get; set; }

    /// <summary>Gets or sets the audit state tracking creation, updates, and soft deletes.</summary>
    public AuditState AuditState { get; set; } = new();

    /// <summary>Gets or sets the concurrency version for optimistic concurrency control.</summary>
    public Guid ConcurrencyVersion { get; set; }

    private City() { } // EF Core

    /// <summary>
    /// Creates a new <see cref="City"/> instance with the specified parameters.
    /// </summary>
    /// <param name="name">The city name.</param>
    /// <param name="country">The country name.</param>
    /// <param name="countryCode">The ISO country code.</param>
    /// <param name="timeZone">The IANA timezone identifier.</param>
    /// <param name="location">The geographic location.</param>
    /// <param name="externalId">The optional external geocoding identifier.</param>
    /// <param name="elevation">The optional elevation in meters.</param>
    /// <returns>A new <see cref="City"/> instance.</returns>
    public static City Create(string name, string country, string countryCode, string timeZone, Location location, long? externalId = null, decimal? elevation = null)
    {
        return new City
        {
            Name = name,
            Country = country,
            CountryCode = countryCode,
            TimeZone = timeZone,
            Location = location,
            ExternalId = externalId,
            Elevation = elevation
        };
    }

    /// <summary>
    /// Changes the city metadata and location.
    /// </summary>
    /// <param name="name">The city name.</param>
    /// <param name="country">The country name.</param>
    /// <param name="countryCode">The ISO country code.</param>
    /// <param name="timeZone">The IANA timezone identifier.</param>
    /// <param name="location">The geographic location.</param>
    /// <param name="elevation">The optional elevation in meters.</param>
    /// <returns>A result containing this city when the change succeeds.</returns>
    public Result<City> ChangeDetails(string name, string country, string countryCode, string timeZone, Location location, decimal? elevation = null)
    {
        this.Name = name;
        this.Country = country;
        this.CountryCode = countryCode;
        this.TimeZone = timeZone;
        this.Location = location;
        this.Elevation = elevation;

        return Result<City>.Success(this);
    }
}
