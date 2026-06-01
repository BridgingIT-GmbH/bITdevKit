// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using System.Text;

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
        IOptions<CoreModuleConfiguration> moduleConfiguration,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        var staleThreshold = TimeSpan.FromMinutes(moduleConfiguration.Value.StaleThresholdMinutes);

        // Enforce subscription plan export limit
        var subscriptionResult = await this.GetOrCreateSubscriptionAsync(userId, cancellationToken);
        if (subscriptionResult.IsFailure)
        {
            return subscriptionResult.Wrap<CityExportResponse>();
        }

        var exportResult = Rule.Check(new SubscriptionExportAllowedRule(subscriptionResult.Value));
        if (exportResult.IsFailure)
        {
            return Result<CityExportResponse>.Failure(exportResult);
        }

        var spec = new UserCitiesByUserSpecification(userId);
        var userCitiesResult = await UserCity.FindAllAsync(spec, null, cancellationToken);
        if (userCitiesResult.IsFailure)
        {
            return userCitiesResult.Wrap<CityExportResponse>();
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
                return cityResult.Wrap<CityExportResponse>();
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
                return weatherResult.Wrap<CityExportResponse>();
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

    private async Task<Result<UserSubscription>> GetOrCreateSubscriptionAsync(string userId, CancellationToken cancellationToken)
    {
        var result = await UserSubscription.FindAllAsync(new SubscriptionByUserSpecification(userId), null, cancellationToken);
        if (result.IsFailure)
        {
            return Result<UserSubscription>.Failure(result);
        }

        var subscriptions = result.Value;
        if (subscriptions.Any())
        {
            var subscription = subscriptions
                .OrderBy(s => s.AuditState.IsDeleted())
                .ThenBy(s => s.StartDate)
                .First();

            if (subscription.AuditState.IsDeleted())
            {
                subscription.Reactivate();
                var updateResult = await subscription.UpsertAsync(cancellationToken);
                if (updateResult.IsFailure)
                {
                    return updateResult.Wrap<UserSubscription>();
                }

                subscription = updateResult.Value.entity;
            }

            if (!subscription.IsActive)
            {
                return Result<UserSubscription>.Failure(new DomainPolicyError(["Subscription is not active."]));
            }

            return Result<UserSubscription>.Success(subscription);
        }

        var newSubscription = UserSubscription.CreateFree(userId);
        var insertResult = await newSubscription.InsertAsync(cancellationToken);
        return insertResult;
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
