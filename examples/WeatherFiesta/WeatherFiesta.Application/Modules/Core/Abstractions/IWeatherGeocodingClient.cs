// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Abstractions;

/// <summary>
/// Abstraction for city geocoding and weather data retrieval.
/// Implemented by the infrastructure layer (e.g., Open-Meteo client).
/// </summary>
public interface IWeatherGeocodingClient
{
    /// <summary>
    /// Searches for cities by name. Returns the first match only.
    /// </summary>
    Task<GeocodingResultModel> SearchCityAsync(string name, string countryCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for cities by name. Returns all matching results.
    /// </summary>
    Task<GeocodingResponseModel> SearchCitiesAsync(string name, string countryCode = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a single geocoding search result.
/// </summary>
public class GeocodingResultModel
{
    /// <summary>Gets or sets the external identifier from the geocoding provider.</summary>
    public long? ExternalId { get; set; }

    /// <summary>Gets or sets the city name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the country name.</summary>
    public string Country { get; set; }

    /// <summary>Gets or sets the ISO country code.</summary>
    public string CountryCode { get; set; }

    /// <summary>Gets or sets the administrative area level 1.</summary>
    public string Admin1 { get; set; }

    /// <summary>Gets or sets the latitude coordinate.</summary>
    public decimal Latitude { get; set; }

    /// <summary>Gets or sets the longitude coordinate.</summary>
    public decimal Longitude { get; set; }

    /// <summary>Gets or sets the IANA timezone identifier.</summary>
    public string TimeZone { get; set; }

    /// <summary>Gets or sets the elevation in meters.</summary>
    public decimal? Elevation { get; set; }
}

/// <summary>
/// Represents the full geocoding response with multiple results.
/// </summary>
public class GeocodingResponseModel
{
    /// <summary>Gets or sets the list of geocoding results.</summary>
    public List<GeocodingResultModel> Results { get; set; } = [];
}
