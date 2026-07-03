// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Query to retrieve weather alerts for all subscribed cities.
/// Alerts are computed at query time using WeatherRuleEngine.EvaluateAlerts().
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class CityAlertsQuery
{
    [Handle]
    private async Task<Result<List<CityAlertsModel>>> HandleAsync(
        IMapper mapper,
        ICurrentUserAccessor currentUserAccessor,
        IOptions<CoreModuleConfiguration> moduleConfiguration,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        var staleThreshold = TimeSpan.FromMinutes(moduleConfiguration.Value.StaleThresholdMinutes);

        var spec = new UserCitiesByUserSpecification(userId);
        var userCitiesResult = await UserCity.FindAllAsync(spec, null, cancellationToken);
        if (userCitiesResult.IsFailure)
        {
            return userCitiesResult.Wrap<List<CityAlertsModel>>();
        }

        var userCities = userCitiesResult.Value;
        var result = new List<CityAlertsModel>();

        foreach (var uc in userCities)
        {
            var weatherSpec = new Specification<CurrentWeather>(cw => cw.CityId == uc.CityId);
            var weatherResult = await CurrentWeather.FindAllAsync(weatherSpec, null, cancellationToken);
            if (weatherResult.IsFailure)
            {
                return weatherResult.Wrap<List<CityAlertsModel>>();
            }

            var weather = weatherResult.Value.FirstOrDefault();
            if (weather is null)
            {
                continue;
            }

            var alerts = WeatherRuleEngine.EvaluateAlerts(
                weather.WeatherCode, weather.WindSpeed, weather.Temperature);

            if (alerts.Any())
            {
                var lastUpdatedText = weather.RetrievedAt.ToRelativeTimeText(
                    DateTime.UtcNow,
                    new RelativeTimeFormatOptions { MinimumUnit = RelativeTimeUnit.Minute });
                var isStale = weather.IsStale(staleThreshold);

                result.Add(new CityAlertsModel
                {
                    CityId = uc.CityId.Value.ToString(),
                    Alerts = mapper.Map<List<WeatherAlert>, List<WeatherAlertModel>>(alerts),
                    LastUpdatedText = lastUpdatedText,
                    StaleDataWarning = isStale,
                    StaleDataWarningMessage = isStale
                        ? $"Data may be outdated - last updated {lastUpdatedText}"
                        : null
                });
            }
        }

        return Result<List<CityAlertsModel>>.Success(result);
    }
}

/// <summary>
/// Response containing weather alerts for a single city.
/// </summary>
public class CityAlertsModel
{
    /// <summary>Gets or sets the city identifier.</summary>
    public string CityId { get; set; }

    /// <summary>Gets or sets the active weather alerts.</summary>
    public List<WeatherAlertModel> Alerts { get; set; } = [];

    /// <summary>Gets or sets human-readable text describing when the weather data was retrieved.</summary>
    public string LastUpdatedText { get; set; }

    /// <summary>Gets or sets a value indicating whether the data may be stale.</summary>
    public bool StaleDataWarning { get; set; }

    /// <summary>Gets or sets the stale data warning message.</summary>
    public string StaleDataWarningMessage { get; set; }
}
