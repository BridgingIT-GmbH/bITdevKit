// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure.OpenMeteo;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a geocoding search result from Open-Meteo.
/// </summary>
public class GeocodingResult
{
    /// <summary>Gets or sets the external identifier from the geocoding provider.</summary>
    [JsonPropertyName("id")]
    public long? ExternalId { get; set; }

    /// <summary>Gets or sets the city name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>Gets or sets the country name.</summary>
    [JsonPropertyName("country")]
    public string Country { get; set; }

    /// <summary>Gets or sets the ISO country code.</summary>
    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; }

    /// <summary>Gets or sets the administrative area level 1.</summary>
    [JsonPropertyName("admin1")]
    public string Admin1 { get; set; }

    /// <summary>Gets or sets the latitude coordinate.</summary>
    [JsonPropertyName("latitude")]
    public decimal Latitude { get; set; }

    /// <summary>Gets or sets the longitude coordinate.</summary>
    [JsonPropertyName("longitude")]
    public decimal Longitude { get; set; }

    /// <summary>Gets or sets the IANA timezone identifier.</summary>
    [JsonPropertyName("timezone")]
    public string TimeZone { get; set; }

    /// <summary>Gets or sets the elevation in meters.</summary>
    [JsonPropertyName("elevation")]
    public decimal? Elevation { get; set; }
}

/// <summary>
/// Represents the full geocoding response with multiple results.
/// </summary>
public class GeocodingResponse
{
    /// <summary>Gets or sets the list of geocoding results.</summary>
    [JsonPropertyName("results")]
    public List<GeocodingResult> Results { get; set; } = [];
}
