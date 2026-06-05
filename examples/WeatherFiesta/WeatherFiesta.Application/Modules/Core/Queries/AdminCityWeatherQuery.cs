// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Admin query to retrieve current weather for a city.
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class AdminCityWeatherQuery
{
    public AdminCityWeatherQuery()
    {
    }

    public AdminCityWeatherQuery(string cityId)
    {
        this.CityId = cityId;
    }

    /// <summary>Gets the city identifier to retrieve current weather for.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    [Handle]
    private async Task<Result<CurrentWeatherModel>> HandleAsync(
        IMapper mapper,
        CancellationToken cancellationToken)
    {
        var cityId = Domain.Modules.Core.Model.CityId.Create(this.CityId);
        var weatherSpec = new Specification<CurrentWeather>(weather => weather.CityId == cityId);
        var weatherResult = await CurrentWeather.FindAllAsync(weatherSpec, null, cancellationToken);
        if (weatherResult.IsFailure)
        {
            return weatherResult.Wrap<CurrentWeatherModel>();
        }

        var weather = weatherResult.Value.FirstOrDefault();
        return weather is null
            ? Result<CurrentWeatherModel>.Failure("Current weather is not available for this city.")
            : Result<CurrentWeatherModel>.Success(mapper.Map<CurrentWeather, CurrentWeatherModel>(weather));
    }
}
