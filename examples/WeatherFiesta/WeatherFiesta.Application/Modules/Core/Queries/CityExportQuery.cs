// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using System.Text;
using BridgingIT.DevKit.Domain;

/// <summary>
/// Query to export current weather for all subscribed cities as CSV.
/// </summary>
[Query]
[HandlerTimeout(10000)]
public partial class CityExportQuery
{
    [Handle]
    private async Task<Result<CityExportResponse>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        // TODO: inject staleThreshold from CoreModuleConfiguration.StaleThresholdMinutes instead of hardcoding
        var staleThreshold = TimeSpan.FromMinutes(60);

        var spec = new UserCitiesByUserSpecification(userId);
        var userCitiesResult = await UserCity.FindAllAsync(spec, null, cancellationToken);
        if (userCitiesResult.IsFailure)
        {
            return Result<CityExportResponse>.Failure(userCitiesResult.Errors.Select(e => e.Message));
        }

        var userCities = userCitiesResult.Value;
        var csv = new StringBuilder();
        // UTF-8 BOM
        csv.Append('\uFEFF');
        csv.AppendLine("City,Country,Temperature(°C),ApparentTemperature(°C),Humidity(%),WeatherCode,WindSpeed(km/h),WindDirection(°),WindGusts(km/h),Precipitation(mm),CloudCover(%),Pressure(hPa),RetrievedAt,StaleDataWarning");

        foreach (var uc in userCities.OrderBy(uc => uc.DisplayOrder))
        {
            var citySpec = new Specification<City>(c => c.Id == uc.CityId);
            var cityResult = await City.FindAllAsync(citySpec, null, cancellationToken);
            if (cityResult.IsFailure)
            {
                return Result<CityExportResponse>.Failure(cityResult.Errors.Select(e => e.Message));
            }

            var city = cityResult.Value.FirstOrDefault();
            if (city is null)
            {
                continue;
            }

            var weatherSpec = new Specification<CurrentWeather>(cw => cw.CityId == uc.CityId);
            var weatherResult = await CurrentWeather.FindAllAsync(weatherSpec, null, cancellationToken);
            if (weatherResult.IsFailure)
            {
                return Result<CityExportResponse>.Failure(weatherResult.Errors.Select(e => e.Message));
            }

            var weather = weatherResult.Value.FirstOrDefault();
            var isStale = weather?.IsStale(staleThreshold) ?? true;
            csv.AppendLine($"\"{city.Name}\",\"{city.Country}\",{weather?.Temperature ?? 0},{weather?.ApparentTemperature ?? 0},{weather?.Humidity ?? 0},{weather?.WeatherCode ?? 0},{weather?.WindSpeed ?? 0},{weather?.WindDirection ?? 0},{weather?.WindGusts ?? 0},{weather?.Precipitation ?? 0},{weather?.CloudCover ?? 0},{weather?.Pressure ?? 0},{weather?.RetrievedAt.ToString("o") ?? ""},{isStale}");
        }

        return Result<CityExportResponse>.Success(new CityExportResponse
        {
            CsvContent = csv.ToString(),
            FileName = $"weather-export-{DateTime.UtcNow:yyyy-MM-dd}.csv",
            ContentType = "text/csv"
        });
    }
}

/// <summary>
/// Response containing exported weather data as CSV.
/// </summary>
public class CityExportResponse
{
    /// <summary>Gets or sets the CSV content.</summary>
    public string CsvContent { get; set; }

    /// <summary>Gets or sets the suggested file name.</summary>
    public string FileName { get; set; }

    /// <summary>Gets or sets the content type.</summary>
    public string ContentType { get; set; }
}
