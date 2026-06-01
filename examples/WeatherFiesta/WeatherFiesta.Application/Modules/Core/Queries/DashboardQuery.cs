// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Query to retrieve the full weather dashboard aggregating primary city,
/// highlights, alerts, and recommendations for all subscribed cities.
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class DashboardQuery
{
    [Handle]
    private async Task<Result<DashboardModel>> HandleAsync(
        IMapper mapper,
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
            return userCitiesResult.Wrap<DashboardModel>();
        }

        var userCities = userCitiesResult.Value;
        if (!userCities.Any())
        {
            return Result<DashboardModel>.Success(new DashboardModel());
        }

        // Primary city
        var primary = userCities.FirstOrDefault(uc => uc.IsPrimary) ?? userCities.OrderBy(uc => uc.DisplayOrder).First();

        // Load weather for all subscribed cities
        var cityWeathers = new Dictionary<string, CurrentWeather>();
        foreach (var uc in userCities)
        {
            var weatherSpec = new Specification<CurrentWeather>(cw => cw.CityId == uc.CityId);
            var weatherResult = await CurrentWeather.FindAllAsync(weatherSpec, null, cancellationToken);
            if (weatherResult.IsFailure)
            {
                return weatherResult.Wrap<DashboardModel>();
            }

            var weather = weatherResult.Value.FirstOrDefault();
            if (weather is not null)
            {
                cityWeathers[uc.CityId.Value.ToString()] = weather;
            }
        }

        // Load unit preferences
        var userProfileSpec = new Specification<UserProfile>(up => up.Id == UserProfileId.Create(Guid.Parse(userId)));
        var userProfileResult = await UserProfile.FindAllAsync(userProfileSpec, null, cancellationToken);
        if (userProfileResult.IsFailure)
        {
            return userProfileResult.Wrap<DashboardModel>();
        }

        var userProfile = userProfileResult.Value.FirstOrDefault();
        var unitPrefs = userProfile is not null
            ? new UnitPreferencesModel
            {
                TemperatureUnit = userProfile.TemperatureUnit.Value,
                TemperatureSymbol = userProfile.TemperatureUnit.Symbol,
                WindSpeedUnit = userProfile.WindSpeedUnit.Value,
                WindSpeedSymbol = userProfile.WindSpeedUnit.Symbol
            }
            : new UnitPreferencesModel();

        // Compute alerts for all cities
        var allAlerts = new List<WeatherAlertModel>();
        foreach (var cw in cityWeathers.Values)
        {
            var alerts = WeatherRuleEngine.EvaluateAlerts(cw.WeatherCode, cw.WindSpeed, cw.Temperature);
            allAlerts.AddRange(alerts.Select(a => new WeatherAlertModel
            {
                Type = a.Type.Value,
                Severity = a.Severity.Value,
                Message = a.Message,
                WeatherCode = a.WeatherCode,
                WindSpeed = a.WindSpeed,
                Temperature = a.Temperature
            }));
        }

        // Compute recommendations for primary city
        var primaryWeather = cityWeathers.GetValueOrDefault(primary.CityId.Value.ToString());
        var recommendations = new List<WeatherRecommendationModel>();
        if (primaryWeather is not null)
        {
            var recs = WeatherRuleEngine.EvaluateRecommendations(
                primaryWeather.WeatherCode, primaryWeather.Temperature, primaryWeather.WindSpeed,
                primaryWeather.Humidity, primaryWeather.Precipitation, 0, 0);
            recommendations = recs.Select(r => new WeatherRecommendationModel
            {
                Category = r.Category.Value,
                Severity = r.Severity.Value,
                Title = r.Title,
                Message = r.Message
            }).ToList();
        }

        // Compute highlights
        DashboardHighlightsModel highlights = null;
        if (cityWeathers.Count >= 2)
        {
            highlights = new DashboardHighlightsModel
            {
                Warmest = MapHighlight(cityWeathers, cw => cw.Temperature, "°C", true),
                Coldest = MapHighlight(cityWeathers, cw => cw.Temperature, "°C", false),
                Wettest = MapHighlight(cityWeathers, cw => cw.Precipitation, "mm", true),
                Windiest = MapHighlight(cityWeathers, cw => cw.WindSpeed, "km/h", true)
            };
        }

        return Result<DashboardModel>.Success(new DashboardModel
        {
            // TODO: City navigation property on UserCityModel is never populated — needs eager loading
            PrimaryCity = mapper.Map<UserCity, UserCityModel>(primary),
            Cities = userCities.OrderBy(uc => uc.DisplayOrder).Select(mapper.Map<UserCity, UserCityModel>).ToList(),
            Highlights = highlights,
            Alerts = allAlerts,
            Recommendations = recommendations,
            UnitPreferences = unitPrefs
        });
    }

    private static CityHighlightModel MapHighlight(
        Dictionary<string, CurrentWeather> cityWeathers,
        Func<CurrentWeather, decimal> selector,
        string unit,
        bool max)
    {
        var selected = max
            ? cityWeathers.MaxBy(kvp => selector(kvp.Value))
            : cityWeathers.MinBy(kvp => selector(kvp.Value));

        return new CityHighlightModel
        {
            CityId = selected.Key,
            Value = selector(selected.Value),
            Unit = unit
        };
    }
}
