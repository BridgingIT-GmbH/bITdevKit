// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using System.Text;

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
        IOptions<CoreModuleConfiguration> moduleConfiguration,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        var cityId = Domain.Modules.Core.Model.CityId.Create(this.CityId);
        var days = this.Days ?? moduleConfiguration.Value.ForecastDays;
        var staleThreshold = TimeSpan.FromMinutes(moduleConfiguration.Value.StaleThresholdMinutes);

        // Verify subscription
        var subSpec = new UserCityByUserAndCitySpecification(userId, cityId);
        var subsResult = await UserCity.FindAllAsync(subSpec, null, cancellationToken);
        if (subsResult.IsFailure)
        {
            return subsResult.Wrap<CityExportResponse>();
        }

        var subs = subsResult.Value;
        if (!subs.Any())
        {
            return Result<CityExportResponse>.Failure("City subscription not found.");
        }

        // Enforce subscription plan export and forecast limits
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

        var maxForecastDays = subscriptionResult.Value.Plan.Details.MaxForecastDays;
        days = maxForecastDays > 0 ? Math.Min(days, maxForecastDays) : days;

        var citySpec = new Specification<City>(c => c.Id == cityId);
        var cityResult = await City.FindAllAsync(citySpec, null, cancellationToken);
        if (cityResult.IsFailure)
        {
            return cityResult.Wrap<CityExportResponse>();
        }

        var city = cityResult.Value.FirstOrDefault();

        var forecastSpec = new Specification<WeatherForecast>(wf => wf.CityId == cityId);
        var forecastsResult = await WeatherForecast.FindAllAsync(forecastSpec, null, cancellationToken);
        if (forecastsResult.IsFailure)
        {
            return forecastsResult.Wrap<CityExportResponse>();
        }

        var forecasts = forecastsResult.Value;
        var csv = new StringBuilder();
        csv.Append('\uFEFF');
        csv.AppendLine("Date,WeatherCode,TemperatureMax(°C),TemperatureMin(°C),PrecipitationSum(mm),WindSpeedMax(km/h),UvIndexMax,Sunrise,Sunset,StaleDataWarning");

        foreach (var f in forecasts.OrderBy(f => f.ForecastDate).Take(days))
        {
            var isStale = f.IsStale(staleThreshold);
            csv.AppendLine($"{f.ForecastDate:yyyy-MM-dd},{f.DayWeatherCode},{f.TemperatureMax},{f.TemperatureMin},{f.PrecipitationSum},{f.WindSpeedMax},{f.UvIndexMax},{f.Sunrise:O},{f.Sunset:O},{isStale}");
        }

        return Result<CityExportResponse>.Success(new CityExportResponse
        {
            CsvContent = csv.ToString(),
            FileName = $"forecast-{city?.Name?.Replace(" ", "-")?.ToLower()}-{DateTime.UtcNow:yyyy-MM-dd}.csv",
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
