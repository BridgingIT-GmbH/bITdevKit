// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Query to retrieve current weather and daily forecast for a subscribed city.
/// Includes staleness warning and unit preferences metadata.
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class CityWeatherQuery
{
    public CityWeatherQuery()
    {
    }

    public CityWeatherQuery(string cityId, int? forecastDays = null)
    {
        this.CityId = cityId;
        this.ForecastDays = forecastDays;
    }

    public CityWeatherQuery(string cityId, string forecastRange)
    {
        this.CityId = cityId;
        this.ForecastRange = forecastRange;
    }

    /// <summary>Gets the city identifier to retrieve weather for.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    /// <summary>Gets the optional number of forecast days to include.</summary>
    public int? ForecastDays { get; private set; }

    /// <summary>Gets the optional ISO forecast date range.</summary>
    public string ForecastRange { get; private set; }

    [Handle]
    private async Task<Result<CityWeatherResponse>> HandleAsync(
        IMapper mapper,
        ICurrentUserAccessor currentUserAccessor,
        IOptions<CoreModuleConfiguration> moduleConfiguration,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        var cityId = Domain.Modules.Core.Model.CityId.Create(this.CityId);
        var staleThreshold = TimeSpan.FromMinutes(moduleConfiguration.Value.StaleThresholdMinutes);

        // Verify subscription
        var subSpec = new UserCityByUserAndCitySpecification(userId, cityId);
        var subscriptionsResult = await UserCity.FindAllAsync(subSpec, null, cancellationToken);
        if (subscriptionsResult.IsFailure)
        {
            return subscriptionsResult.Wrap<CityWeatherResponse>();
        }

        var subscriptions = subscriptionsResult.Value;
        if (!subscriptions.Any())
        {
            return Result<CityWeatherResponse>.Failure("City subscription not found.");
        }

        // Load user subscription for plan enforcement
        var subscriptionResult = await this.GetOrCreateSubscriptionAsync(userId, cancellationToken);
        if (subscriptionResult.IsFailure)
        {
            return subscriptionResult.Wrap<CityWeatherResponse>();
        }

        var requestedDays = this.ForecastDays ?? moduleConfiguration.Value.ForecastDays;
        var maxForecastDays = subscriptionResult.Value.Plan.Details.MaxForecastDays;
        var forecastPeriodResult = ForecastPeriod.Resolve(this.ForecastRange, requestedDays);
        if (forecastPeriodResult.IsFailure)
        {
            return forecastPeriodResult.Wrap<CityWeatherResponse>();
        }

        var forecastPeriod = ForecastPeriod.Limit(forecastPeriodResult.Value, maxForecastDays);

        // Load current weather
        var weatherSpec = new Specification<CurrentWeather>(cw => cw.CityId == cityId);
        var currentWeatherResult = await CurrentWeather.FindAllAsync(weatherSpec, null, cancellationToken);
        if (currentWeatherResult.IsFailure)
        {
            return currentWeatherResult.Wrap<CityWeatherResponse>();
        }

        var currentWeather = currentWeatherResult.Value.FirstOrDefault();

        // Load forecasts
        var forecastSpec = new Specification<WeatherForecast>(wf => wf.CityId == cityId);
        var forecastsResult = await WeatherForecast.FindAllAsync(forecastSpec, null, cancellationToken);
        if (forecastsResult.IsFailure)
        {
            return forecastsResult.Wrap<CityWeatherResponse>();
        }

        var forecasts = forecastsResult.Value
            .Where(forecast => forecastPeriod.Contains(forecast.ForecastDate))
            .OrderBy(forecast => forecast.ForecastDate)
            .ToList();

        // Load unit preferences
        var userProfileSpec = new UserProfileByUserSpecification(userId);
        var userProfileResult = await UserProfile.FindAllAsync(userProfileSpec, null, cancellationToken);
        if (userProfileResult.IsFailure)
        {
            return userProfileResult.Wrap<CityWeatherResponse>();
        }

        var userProfile = userProfileResult.Value.FirstOrDefault();

        var currentWeatherModel = currentWeather is not null
            ? mapper.Map<CurrentWeather, CurrentWeatherModel>(currentWeather)
            : null;
        var response = new CityWeatherResponse
        {
            CurrentWeather = currentWeatherModel,
            ForecastPeriod = forecastPeriod.ToIsoRangeString(),
            Forecasts = forecasts.Select(mapper.Map<WeatherForecast, WeatherForecastModel>).ToList(),
            UnitPreferences = userProfile is not null
                ? new UnitPreferencesModel
                {
                    TemperatureUnit = userProfile.TemperatureUnit.Value,
                    TemperatureSymbol = userProfile.TemperatureUnit.Symbol,
                    WindSpeedUnit = userProfile.WindSpeedUnit.Value,
                    WindSpeedSymbol = userProfile.WindSpeedUnit.Symbol
                }
                : new UnitPreferencesModel()
        };

        // Staleness
        if (currentWeather is not null && currentWeather.IsStale(staleThreshold))
        {
            response.StaleDataWarning = true;
            response.StaleDataWarningMessage = $"Data may be outdated - last updated {currentWeatherModel.LastUpdatedText}";
            currentWeatherModel.StaleDataWarning = true;
            currentWeatherModel.StaleDataWarningMessage = response.StaleDataWarningMessage;
        }

        return Result<CityWeatherResponse>.Success(response);
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
/// Response containing current weather, forecasts, and unit preferences for a city.
/// </summary>
public class CityWeatherResponse
{
    /// <summary>Gets or sets the current weather conditions.</summary>
    public CurrentWeatherModel CurrentWeather { get; set; }

    /// <summary>Gets or sets the ISO interval used to select daily forecasts.</summary>
    public string ForecastPeriod { get; set; }

    /// <summary>Gets or sets the daily forecasts.</summary>
    public List<WeatherForecastModel> Forecasts { get; set; } = [];

    /// <summary>Gets or sets the user's unit preferences.</summary>
    public UnitPreferencesModel UnitPreferences { get; set; }

    /// <summary>Gets or sets a value indicating whether the data may be stale.</summary>
    public bool StaleDataWarning { get; set; }

    /// <summary>Gets or sets the stale data warning message.</summary>
    public string StaleDataWarningMessage { get; set; }
}
