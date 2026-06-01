// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Query to retrieve all city subscriptions for the current user,
/// including current weather data and staleness warnings.
/// Staleness is computed at query time per ADR-0002.
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class UserCitiesQuery
{
    [Handle]
    private async Task<Result<List<UserCityModel>>> HandleAsync(
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
            return userCitiesResult.Wrap<List<UserCityModel>>();
        }

        var userCities = userCitiesResult.Value;
        var result = new List<UserCityModel>();

        foreach (var uc in userCities.OrderBy(uc => uc.DisplayOrder))
        {
            var model = mapper.Map<UserCity, UserCityModel>(uc);
            // TODO: City navigation property is never populated — needs eager loading of City entity

            // Load current weather for the city
            var weatherSpec = new Specification<CurrentWeather>(cw => cw.CityId == uc.CityId);
            var weatherResult = await CurrentWeather.FindAllAsync(weatherSpec, null, cancellationToken);
            if (weatherResult.IsFailure)
            {
                return weatherResult.Wrap<List<UserCityModel>>();
            }

            var weather = weatherResult.Value.FirstOrDefault();
            if (weather is not null)
            {
                model.CurrentWeather = mapper.Map<CurrentWeather, CurrentWeatherModel>(weather);
                model.StaleDataWarning = weather.IsStale(staleThreshold);
                if (model.StaleDataWarning)
                {
                    var minutesSinceUpdate = (int)(DateTime.UtcNow - weather.RetrievedAt).TotalMinutes;
                    model.StaleDataWarningMessage = $"Data may be outdated — last updated {minutesSinceUpdate} minutes ago";
                }
            }

            result.Add(model);
        }

        return Result<List<UserCityModel>>.Success(result);
    }
}
