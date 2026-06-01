// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

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

    /// <summary>Gets the city identifier to retrieve weather for.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    /// <summary>Gets the optional number of forecast days to include.</summary>
    public int? ForecastDays { get; private set; }

    [Handle]
    private async Task<Result<CityWeatherResponse>> HandleAsync(
        IMapper mapper,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        var cityId = Domain.Model.CityId.Create(this.CityId);
        // TODO: inject staleThreshold from CoreModuleConfiguration.StaleThresholdMinutes instead of hardcoding
        var staleThreshold = TimeSpan.FromMinutes(60);

        // Verify subscription
        var subSpec = new UserCityByUserAndCitySpecification(userId, cityId);
        var subscriptionsResult = await UserCity.FindAllAsync(subSpec, null, cancellationToken);
        if (subscriptionsResult.IsFailure)
        {
            return Result<CityWeatherResponse>.Failure(subscriptionsResult.Errors.Select(e => e.Message));
        }

        var subscriptions = subscriptionsResult.Value;
        if (!subscriptions.Any())
        {
            return Result<CityWeatherResponse>.Failure("City subscription not found.");
        }

        // Load current weather
        var weatherSpec = new Specification<CurrentWeather>(cw => cw.CityId == cityId);
        var currentWeatherResult = await CurrentWeather.FindAllAsync(weatherSpec, null, cancellationToken);
        if (currentWeatherResult.IsFailure)
        {
            return Result<CityWeatherResponse>.Failure(currentWeatherResult.Errors.Select(e => e.Message));
        }

        var currentWeather = currentWeatherResult.Value.FirstOrDefault();

        // Load forecasts
        var forecastSpec = new Specification<WeatherForecast>(wf => wf.CityId == cityId);
        var forecastsResult = await WeatherForecast.FindAllAsync(forecastSpec, null, cancellationToken);
        if (forecastsResult.IsFailure)
        {
            return Result<CityWeatherResponse>.Failure(forecastsResult.Errors.Select(e => e.Message));
        }

        var forecasts = forecastsResult.Value;

        // Load unit preferences
        var userProfileSpec = new Specification<UserProfile>(up => up.Id == UserProfileId.Create(Guid.Parse(userId)));
        var userProfileResult = await UserProfile.FindAllAsync(userProfileSpec, null, cancellationToken);
        if (userProfileResult.IsFailure)
        {
            return Result<CityWeatherResponse>.Failure(userProfileResult.Errors.Select(e => e.Message));
        }

        var userProfile = userProfileResult.Value.FirstOrDefault();

        var response = new CityWeatherResponse
        {
            CurrentWeather = currentWeather is not null ? mapper.Map<CurrentWeather, CurrentWeatherModel>(currentWeather) : null,
            Forecasts = forecasts.OrderBy(f => f.ForecastDate).Select(mapper.Map<WeatherForecast, WeatherForecastModel>).ToList(),
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
            var minutes = (int)(DateTime.UtcNow - currentWeather.RetrievedAt).TotalMinutes;
            response.StaleDataWarning = true;
            response.StaleDataWarningMessage = $"Data may be outdated — last updated {minutes} minutes ago";
        }

        return Result<CityWeatherResponse>.Success(response);
    }
}

/// <summary>
/// Response containing current weather, forecasts, and unit preferences for a city.
/// </summary>
public class CityWeatherResponse
{
    /// <summary>Gets or sets the current weather conditions.</summary>
    public CurrentWeatherModel CurrentWeather { get; set; }

    /// <summary>Gets or sets the daily forecasts.</summary>
    public List<WeatherForecastModel> Forecasts { get; set; } = [];

    /// <summary>Gets or sets the user's unit preferences.</summary>
    public UnitPreferencesModel UnitPreferences { get; set; }

    /// <summary>Gets or sets a value indicating whether the data may be stale.</summary>
    public bool StaleDataWarning { get; set; }

    /// <summary>Gets or sets the stale data warning message.</summary>
    public string StaleDataWarningMessage { get; set; }
}
