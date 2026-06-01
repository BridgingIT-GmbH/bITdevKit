// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

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
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        var cityId = Domain.Model.CityId.Create(this.CityId);
        // TODO: inject staleThreshold from CoreModuleConfiguration.StaleThresholdMinutes instead of hardcoding
        var staleThreshold = TimeSpan.FromMinutes(60);

        // Verify subscription
        var subSpec = new UserCityByUserAndCitySpecification(userId, cityId);
        var subsResult = await UserCity.FindAllAsync(subSpec, null, cancellationToken);
        if (subsResult.IsFailure)
        {
            return Result<CityRecommendationsResponse>.Failure(subsResult.Errors.Select(e => e.Message));
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
            return Result<CityRecommendationsResponse>.Failure(weatherResult.Errors.Select(e => e.Message));
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
            return Result<CityRecommendationsResponse>.Failure(forecastResult.Errors.Select(e => e.Message));
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
        var userProfileSpec = new Specification<UserProfile>(up => up.Id == UserProfileId.Create(Guid.Parse(userId)));
        var userProfileResult = await UserProfile.FindAllAsync(userProfileSpec, null, cancellationToken);
        if (userProfileResult.IsFailure)
        {
            return Result<CityRecommendationsResponse>.Failure(userProfileResult.Errors.Select(e => e.Message));
        }

        var userProfile = userProfileResult.Value.FirstOrDefault();

        return Result<CityRecommendationsResponse>.Success(new CityRecommendationsResponse
        {
            CityId = this.CityId,
            Recommendations = recommendations.Select(r => new WeatherRecommendationModel
            {
                Category = r.Category.Value,
                Severity = r.Severity.Value,
                Title = r.Title,
                Message = r.Message
            }).ToList(),
            StaleDataWarning = weather.IsStale(staleThreshold),
            StaleDataWarningMessage = weather.IsStale(staleThreshold)
                ? $"Data may be outdated — last updated {(int)(DateTime.UtcNow - weather.RetrievedAt).TotalMinutes} minutes ago"
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

    /// <summary>Gets or sets a value indicating whether the data may be stale.</summary>
    public bool StaleDataWarning { get; set; }

    /// <summary>Gets or sets the stale data warning message.</summary>
    public string StaleDataWarningMessage { get; set; }

    /// <summary>Gets or sets the user's unit preferences.</summary>
    public UnitPreferencesModel UnitPreferences { get; set; }
}
