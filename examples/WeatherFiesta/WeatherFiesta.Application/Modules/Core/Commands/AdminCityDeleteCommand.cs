// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Admin command to hard-delete a city and all associated data.
/// </summary>
[Command]
[HandlerTimeout(15000)]
public partial class AdminCityDeleteCommand
{
    public AdminCityDeleteCommand()
    {
    }

    public AdminCityDeleteCommand(string cityId)
    {
        this.CityId = cityId;
    }

    /// <summary>Gets the city identifier to delete.</summary>
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
            return cityResult.Wrap<Unit>();
        }

        var city = cityResult.Value.FirstOrDefault();
        if (city is null)
        {
            return Result<Unit>.Failure("City not found.");
        }

        // Delete weather forecasts
        var forecastSpec = new Specification<WeatherForecast>(wf => wf.CityId == cityId);
        var forecastsResult = await WeatherForecast.FindAllAsync(forecastSpec, null, cancellationToken);
        if (forecastsResult.IsFailure)
        {
            return forecastsResult.Wrap<Unit>();
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
            return weathersResult.Wrap<Unit>();
        }

        foreach (var weather in weathersResult.Value)
        {
            await weather.DeleteAsync(cancellationToken);
        }

        // Delete user subscriptions (including soft-deleted)
        var userCitySpec = new Specification<UserCity>(uc => uc.CityId == cityId);
        var userCitiesResult = await UserCity.FindAllAsync(userCitySpec, null, cancellationToken);
        if (userCitiesResult.IsFailure)
        {
            return userCitiesResult.Wrap<Unit>();
        }

        foreach (var userCity in userCitiesResult.Value)
        {
            await userCity.DeleteAsync(cancellationToken);
        }

        // Delete the city
        var result = await city.DeleteAsync(cancellationToken);
        if (result.IsFailure)
        {
            return Result<Unit>.Failure(result);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
