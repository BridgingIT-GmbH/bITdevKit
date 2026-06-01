// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure.OpenMeteo;

/// <summary>
/// Client abstraction for Open-Meteo weather and geocoding APIs.
/// </summary>
public interface IOpenMeteoClient
{
    /// <summary>
    /// Searches for cities by name using the Open-Meteo Geocoding API.
    /// Returns the first match only.
    /// </summary>
    Task<GeocodingResult> SearchCitiesAsync(string name, string countryCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for cities by name using the Open-Meteo Geocoding API.
    /// Returns all matching results.
    /// </summary>
    Task<GeocodingResponse> SearchCitiesAllAsync(string name, string countryCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a specific city by its Open-Meteo external ID.
    /// </summary>
    Task<GeocodingResult> LookupCityAsync(long externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches current weather and forecast data for a given location.
    /// </summary>
    Task<WeatherData> GetWeatherAsync(decimal latitude, decimal longitude, string timeZone, int forecastDays, CancellationToken cancellationToken = default);
}
