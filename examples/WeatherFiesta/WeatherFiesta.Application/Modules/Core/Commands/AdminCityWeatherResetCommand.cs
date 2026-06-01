// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Admin command to delete all weather data for a city.
/// </summary>
[Command]
[HandlerTimeout(15000)]
public partial class AdminCityWeatherResetCommand
{
    public AdminCityWeatherResetCommand()
    {
    }

    public AdminCityWeatherResetCommand(string cityId)
    {
        this.CityId = cityId;
    }

    /// <summary>Gets the city identifier to reset weather data for.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    [Handle]
    private async Task<Result<Unit>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var cityId = Domain.Model.CityId.Create(this.CityId);

        var spec = new Specification<City>(c => c.Id == cityId);
        var cityResult = await City.FindAllAsync(spec, null, cancellationToken);
        if (cityResult.IsFailure)
        {
            return Result<Unit>.Failure(cityResult.Errors.Select(e => e.Message));
        }

        var city = cityResult.Value.FirstOrDefault();
        if (city is null)
        {
            return Result<Unit>.Failure("City not found.");
        }

        // Delete forecasts
        var forecastSpec = new Specification<WeatherForecast>(wf => wf.CityId == cityId);
        var forecastsResult = await WeatherForecast.FindAllAsync(forecastSpec, null, cancellationToken);
        if (forecastsResult.IsFailure)
        {
            return Result<Unit>.Failure(forecastsResult.Errors.Select(e => e.Message));
        }

        foreach (var forecast in forecastsResult.Value)
        {
            await forecast.DeleteAsync(cancellationToken);
        }

        // Delete current weather
        var weatherSpec = new Specification<CurrentWeather>(cw => cw.CityId == cityId);
        var weathersResult = await CurrentWeather.FindAllAsync(weatherSpec, null, cancellationToken);
        if (weathersResult.IsFailure)
        {
            return Result<Unit>.Failure(weathersResult.Errors.Select(e => e.Message));
        }

        foreach (var weather in weathersResult.Value)
        {
            await weather.DeleteAsync(cancellationToken);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
