// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using System.Text;
using BridgingIT.DevKit.Domain;

/// <summary>
/// Query to export forecast data for a specific subscribed city as CSV.
/// </summary>
[Query]
[HandlerTimeout(10000)]
public partial class CityWeatherExportQuery
{
    public CityWeatherExportQuery()
    {
    }

    public CityWeatherExportQuery(string cityId, int? days = null)
    {
        this.CityId = cityId;
        this.Days = days;
    }

    /// <summary>Gets the city identifier to export forecast data for.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    /// <summary>Gets the optional number of days to export.</summary>
    public int? Days { get; private set; }

    [Handle]
    private async Task<Result<CityExportResponse>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        var cityId = Domain.Model.CityId.Create(this.CityId);
        var days = this.Days ?? 7;

        // Verify subscription
        var subSpec = new UserCityByUserAndCitySpecification(userId, cityId);
        var subsResult = await UserCity.FindAllAsync(subSpec, null, cancellationToken);
        if (subsResult.IsFailure)
        {
            return Result<CityExportResponse>.Failure(subsResult.Errors.Select(e => e.Message));
        }

        var subs = subsResult.Value;
        if (!subs.Any())
        {
            return Result<CityExportResponse>.Failure("City subscription not found.");
        }

        var citySpec = new Specification<City>(c => c.Id == cityId);
        var cityResult = await City.FindAllAsync(citySpec, null, cancellationToken);
        if (cityResult.IsFailure)
        {
            return Result<CityExportResponse>.Failure(cityResult.Errors.Select(e => e.Message));
        }

        var city = cityResult.Value.FirstOrDefault();

        var forecastSpec = new Specification<WeatherForecast>(wf => wf.CityId == cityId);
        var forecastsResult = await WeatherForecast.FindAllAsync(forecastSpec, null, cancellationToken);
        if (forecastsResult.IsFailure)
        {
            return Result<CityExportResponse>.Failure(forecastsResult.Errors.Select(e => e.Message));
        }

        var forecasts = forecastsResult.Value;
        var csv = new StringBuilder();
        csv.Append('\uFEFF');
        csv.AppendLine("Date,WeatherCode,TemperatureMax(°C),TemperatureMin(°C),PrecipitationSum(mm),WindSpeedMax(km/h),UvIndexMax,Sunrise,Sunset,StaleDataWarning");

        foreach (var f in forecasts.OrderBy(f => f.ForecastDate).Take(days))
        {
            // TODO: inject staleThreshold from CoreModuleConfiguration.StaleThresholdMinutes instead of hardcoding
            var isStale = f.IsStale(TimeSpan.FromMinutes(60));
            csv.AppendLine($"{f.ForecastDate:yyyy-MM-dd},{f.DayWeatherCode},{f.TemperatureMax},{f.TemperatureMin},{f.PrecipitationSum},{f.WindSpeedMax},{f.UvIndexMax},{f.Sunrise:O},{f.Sunset:O},{isStale}");
        }

        return Result<CityExportResponse>.Success(new CityExportResponse
        {
            CsvContent = csv.ToString(),
            FileName = $"forecast-{city?.Name?.Replace(" ", "-")?.ToLower()}-{DateTime.UtcNow:yyyy-MM-dd}.csv",
            ContentType = "text/csv"
        });
    }
}
