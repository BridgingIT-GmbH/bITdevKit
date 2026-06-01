// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure.OpenMeteo;

using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Client that implements the Application-level <see cref="IWeatherGeocodingClient"/>
/// using the Infrastructure-level <see cref="IOpenMeteoClient"/>.
/// </summary>
public class OpenMeteoGeocodingClient : IWeatherGeocodingClient
{
    private readonly IOpenMeteoClient openMeteoClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenMeteoGeocodingClient"/> class.
    /// </summary>
    /// <param name="openMeteoClient">The Open-Meteo client to delegate to.</param>
    public OpenMeteoGeocodingClient(IOpenMeteoClient openMeteoClient)
    {
        this.openMeteoClient = openMeteoClient;
    }

    /// <inheritdoc />
    public async Task<GeocodingResultModel> SearchCityAsync(
        string name,
        string countryCode = null,
        CancellationToken cancellationToken = default)
    {
        var result = await this.openMeteoClient.SearchCitiesAsync(name, countryCode, cancellationToken);
        return MapResult(result);
    }

    /// <inheritdoc />
    public async Task<GeocodingResponseModel> SearchCitiesAsync(
        string name,
        string countryCode = null,
        CancellationToken cancellationToken = default)
    {
        var response = await this.openMeteoClient.SearchCitiesAllAsync(name, countryCode, cancellationToken);
        return MapResponse(response);
    }

    private static GeocodingResultModel MapResult(GeocodingResult source)
    {
        if (source is null)
        {
            return null;
        }

        return new GeocodingResultModel
        {
            ExternalId = source.ExternalId,
            Name = source.Name,
            Country = source.Country,
            CountryCode = source.CountryCode,
            Admin1 = source.Admin1,
            Latitude = source.Latitude,
            Longitude = source.Longitude,
            TimeZone = source.TimeZone,
            Elevation = source.Elevation
        };
    }

    private static GeocodingResponseModel MapResponse(GeocodingResponse source)
    {
        if (source is null)
        {
            return new GeocodingResponseModel();
        }

        return new GeocodingResponseModel
        {
            Results = source.Results?.Select(MapResult).ToList() ?? []
        };
    }
}
