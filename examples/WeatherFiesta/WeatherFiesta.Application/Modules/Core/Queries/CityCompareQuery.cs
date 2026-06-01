// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Query to compare current weather across multiple subscribed cities.
/// Returns weather data for each city with comparative highlights.
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class CityCompareQuery
{
    /// <summary>Gets or sets the list of city identifiers to compare (2-10 cities).</summary>
    [ValidateNotNull]
    public List<string> CityIds { get; set; }

    [Validate]
    private static void Validate(InlineValidator<CityCompareQuery> validator)
    {
        validator.RuleFor(c => c.CityIds).Must(ids => ids.Count >= 2 && ids.Count <= 10)
            .WithMessage("Must compare between 2 and 10 cities.");
    }

    [Handle]
    private async Task<Result<CityCompareResponse>> HandleAsync(
        IMapper mapper,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        // TODO: inject staleThreshold from CoreModuleConfiguration.StaleThresholdMinutes instead of hardcoding
        var staleThreshold = TimeSpan.FromMinutes(60);

        var result = new CityCompareResponse();

        foreach (var cityIdStr in this.CityIds)
        {
            var cityId = Domain.Model.CityId.Create(cityIdStr);

            // Verify subscription
            var subSpec = new UserCityByUserAndCitySpecification(userId, cityId);
            var subsResult = await UserCity.FindAllAsync(subSpec, null, cancellationToken);
            if (subsResult.IsFailure)
            {
                return Result<CityCompareResponse>.Failure(subsResult.Errors.Select(e => e.Message));
            }

            var subs = subsResult.Value;
            if (!subs.Any())
            {
                continue;
            }

            var weatherSpec = new Specification<CurrentWeather>(cw => cw.CityId == cityId);
            var weatherResult = await CurrentWeather.FindAllAsync(weatherSpec, null, cancellationToken);
            if (weatherResult.IsFailure)
            {
                return Result<CityCompareResponse>.Failure(weatherResult.Errors.Select(e => e.Message));
            }

            var weather = weatherResult.Value.FirstOrDefault();

            var cityComparison = new CityComparisonModel
            {
                CityId = cityIdStr,
                CurrentWeather = weather is not null ? mapper.Map<CurrentWeather, CurrentWeatherModel>(weather) : null,
                StaleDataWarning = weather is not null && weather.IsStale(staleThreshold),
                StaleDataWarningMessage = weather is not null && weather.IsStale(staleThreshold)
                    ? $"Data may be outdated — last updated {(int)(DateTime.UtcNow - weather.RetrievedAt).TotalMinutes} minutes ago"
                    : null
            };

            result.Cities.Add(cityComparison);
        }

        // Load unit preferences
        var userProfileSpec = new Specification<UserProfile>(up => up.Id == UserProfileId.Create(Guid.Parse(userId)));
        var userProfileResult = await UserProfile.FindAllAsync(userProfileSpec, null, cancellationToken);
        if (userProfileResult.IsFailure)
        {
            return Result<CityCompareResponse>.Failure(userProfileResult.Errors.Select(e => e.Message));
        }

        var userProfile = userProfileResult.Value.FirstOrDefault();
        result.UnitPreferences = userProfile is not null
            ? new UnitPreferencesModel
            {
                TemperatureUnit = userProfile.TemperatureUnit.Value,
                TemperatureSymbol = userProfile.TemperatureUnit.Symbol,
                WindSpeedUnit = userProfile.WindSpeedUnit.Value,
                WindSpeedSymbol = userProfile.WindSpeedUnit.Symbol
            }
            : new UnitPreferencesModel();

        // Compute highlights
        var withWeather = result.Cities.Where(c => c.CurrentWeather is not null).ToList();
        if (withWeather.Count >= 2)
        {
            result.Highlights = new DashboardHighlightsModel
            {
                Warmest = new CityHighlightModel
                {
                    CityId = withWeather.MaxBy(c => c.CurrentWeather.Temperature)?.CityId,
                    Value = withWeather.MaxBy(c => c.CurrentWeather.Temperature)?.CurrentWeather?.Temperature ?? 0,
                    Unit = "°C"
                },
                Coldest = new CityHighlightModel
                {
                    CityId = withWeather.MinBy(c => c.CurrentWeather.Temperature)?.CityId,
                    Value = withWeather.MinBy(c => c.CurrentWeather.Temperature)?.CurrentWeather?.Temperature ?? 0,
                    Unit = "°C"
                },
                Wettest = new CityHighlightModel
                {
                    CityId = withWeather.MaxBy(c => c.CurrentWeather.Precipitation)?.CityId,
                    Value = withWeather.MaxBy(c => c.CurrentWeather.Precipitation)?.CurrentWeather?.Precipitation ?? 0,
                    Unit = "mm"
                },
                Windiest = new CityHighlightModel
                {
                    CityId = withWeather.MaxBy(c => c.CurrentWeather.WindSpeed)?.CityId,
                    Value = withWeather.MaxBy(c => c.CurrentWeather.WindSpeed)?.CurrentWeather?.WindSpeed ?? 0,
                    Unit = "km/h"
                }
            };
        }

        return Result<CityCompareResponse>.Success(result);
    }
}

/// <summary>
/// Response containing weather comparison data across multiple cities.
/// </summary>
public class CityCompareResponse
{
    /// <summary>Gets or sets the weather data for each compared city.</summary>
    public List<CityComparisonModel> Cities { get; set; } = [];

    /// <summary>Gets or sets the comparative highlights.</summary>
    public DashboardHighlightsModel Highlights { get; set; }

    /// <summary>Gets or sets the user's unit preferences.</summary>
    public UnitPreferencesModel UnitPreferences { get; set; }
}

/// <summary>
/// DTO representing weather data for a single city in a comparison.
/// </summary>
public class CityComparisonModel
{
    /// <summary>Gets or sets the city identifier.</summary>
    public string CityId { get; set; }

    /// <summary>Gets or sets the current weather.</summary>
    public CurrentWeatherModel CurrentWeather { get; set; }

    /// <summary>Gets or sets a value indicating whether the data may be stale.</summary>
    public bool StaleDataWarning { get; set; }

    /// <summary>Gets or sets the stale data warning message.</summary>
    public string StaleDataWarningMessage { get; set; }
}
