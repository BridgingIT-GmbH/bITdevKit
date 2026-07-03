// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Query to retrieve weather recommendations for a subscribed city.
/// Recommendations are computed at query time using WeatherRuleEngine.EvaluateRecommendations().
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class CityRecommendationsQuery
{
    public CityRecommendationsQuery()
    {
    }

    public CityRecommendationsQuery(string cityId)
    {
        this.CityId = cityId;
    }

    /// <summary>Gets the city identifier to retrieve recommendations for.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    [Handle]
    private async Task<Result<CityRecommendationsResponse>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        IOptions<CoreModuleConfiguration> moduleConfiguration,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        var cityId = Domain.Modules.Core.Model.CityId.Create(this.CityId);
        var staleThreshold = TimeSpan.FromMinutes(moduleConfiguration.Value.StaleThresholdMinutes);

        // Verify subscription
        var subSpec = new UserCityByUserAndCitySpecification(userId, cityId);
        var subsResult = await UserCity.FindAllAsync(subSpec, null, cancellationToken);
        if (subsResult.IsFailure)
        {
            return subsResult.Wrap<CityRecommendationsResponse>();
        }

        var subs = subsResult.Value;
        if (!subs.Any())
        {
            return Result<CityRecommendationsResponse>.Failure("City subscription not found.");
        }

        var weatherSpec = new Specification<CurrentWeather>(cw => cw.CityId == cityId);
        var weatherResult = await CurrentWeather.FindAllAsync(weatherSpec, null, cancellationToken);
        if (weatherResult.IsFailure)
        {
            return weatherResult.Wrap<CityRecommendationsResponse>();
        }

        var weather = weatherResult.Value.FirstOrDefault();
        if (weather is null)
        {
            return Result<CityRecommendationsResponse>.Success(new CityRecommendationsResponse());
        }

        // Get today's forecast for UV index
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var forecastSpec = new Specification<WeatherForecast>(wf => wf.CityId == cityId && wf.ForecastDate == today);
        var forecastResult = await WeatherForecast.FindAllAsync(forecastSpec, null, cancellationToken);
        if (forecastResult.IsFailure)
        {
            return forecastResult.Wrap<CityRecommendationsResponse>();
        }

        var todayForecast = forecastResult.Value.FirstOrDefault();

        var recommendations = WeatherRuleEngine.EvaluateRecommendations(
            weather.WeatherCode,
            weather.Temperature,
            weather.WindSpeed,
            weather.Humidity,
            weather.Precipitation,
            todayForecast?.PrecipitationProbabilityMax ?? 0,
            todayForecast?.UvIndexMax ?? 0);

        // Load unit preferences
        var userProfileSpec = new UserProfileByUserSpecification(userId);
        var userProfileResult = await UserProfile.FindAllAsync(userProfileSpec, null, cancellationToken);
        if (userProfileResult.IsFailure)
        {
            return userProfileResult.Wrap<CityRecommendationsResponse>();
        }

        var userProfile = userProfileResult.Value.FirstOrDefault();
        var lastUpdatedText = weather.RetrievedAt.ToRelativeTimeText(
            DateTime.UtcNow,
            new RelativeTimeFormatOptions { MinimumUnit = RelativeTimeUnit.Minute });
        var isStale = weather.IsStale(staleThreshold);

        return Result<CityRecommendationsResponse>.Success(new CityRecommendationsResponse
        {
            CityId = this.CityId,
            Recommendations = recommendations.ConvertAll(r => new WeatherRecommendationModel
            {
                Category = r.Category.Value,
                Severity = r.Severity.Value,
                Title = r.Title,
                Message = r.Message
            }),
            LastUpdatedText = lastUpdatedText,
            StaleDataWarning = isStale,
            StaleDataWarningMessage = isStale
                ? $"Data may be outdated - last updated {lastUpdatedText}"
                : null,
            UnitPreferences = userProfile is not null
                ? new UnitPreferencesModel
                {
                    TemperatureUnit = userProfile.TemperatureUnit.Value,
                    TemperatureSymbol = userProfile.TemperatureUnit.Symbol,
                    WindSpeedUnit = userProfile.WindSpeedUnit.Value,
                    WindSpeedSymbol = userProfile.WindSpeedUnit.Symbol
                }
                : new UnitPreferencesModel()
        });
    }
}

/// <summary>
/// Response containing weather recommendations for a city.
/// </summary>
public class CityRecommendationsResponse
{
    /// <summary>Gets or sets the city identifier.</summary>
    public string CityId { get; set; }

    /// <summary>Gets or sets the weather recommendations.</summary>
    public List<WeatherRecommendationModel> Recommendations { get; set; } = [];

    /// <summary>Gets or sets human-readable text describing when the weather data was retrieved.</summary>
    public string LastUpdatedText { get; set; }

    /// <summary>Gets or sets a value indicating whether the data may be stale.</summary>
    public bool StaleDataWarning { get; set; }

    /// <summary>Gets or sets the stale data warning message.</summary>
    public string StaleDataWarningMessage { get; set; }

    /// <summary>Gets or sets the user's unit preferences.</summary>
    public UnitPreferencesModel UnitPreferences { get; set; }
}
