// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Query to retrieve sunrise/sunset data for a subscribed city.
/// Returns sun data for the specified number of days from the forecast.
/// </summary>
[Query]
[HandlerTimeout(5000)]
public partial class CitySunQuery
{
    public CitySunQuery()
    {
    }

    public CitySunQuery(string cityId, int? days = null)
    {
        this.CityId = cityId;
        this.Days = days;
    }

    /// <summary>Gets the city identifier to retrieve sun data for.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    /// <summary>Gets the optional number of days to include.</summary>
    public int? Days { get; private set; }

    [Handle]
    private async Task<Result<CitySunResponse>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        var cityId = Domain.Modules.Core.Model.CityId.Create(this.CityId);
        var days = this.Days ?? 1;

        // Verify subscription
        var subSpec = new UserCityByUserAndCitySpecification(userId, cityId);
        var subscriptionsResult = await UserCity.FindAllAsync(subSpec, null, cancellationToken);
        if (subscriptionsResult.IsFailure)
        {
            return subscriptionsResult.Wrap<CitySunResponse>();
        }

        var subscriptions = subscriptionsResult.Value;
        if (!subscriptions.Any())
        {
            return Result<CitySunResponse>.Failure("City subscription not found.");
        }

        var forecastSpec = new Specification<WeatherForecast>(wf => wf.CityId == cityId);
        var forecastsResult = await WeatherForecast.FindAllAsync(forecastSpec, null, cancellationToken);
        if (forecastsResult.IsFailure)
        {
            return forecastsResult.Wrap<CitySunResponse>();
        }

        var forecasts = forecastsResult.Value;
        var sunData = forecasts
            .OrderBy(f => f.ForecastDate)
            .Take(days)
            .Select(f => new SunDataModel
            {
                Date = f.ForecastDate,
                Sunrise = f.Sunrise,
                Sunset = f.Sunset,
                DaylightDurationSeconds = f.DaylightDurationSeconds,
                IsDay = DateTime.UtcNow >= f.Sunrise && DateTime.UtcNow <= f.Sunset
            }).ToList();

        return Result<CitySunResponse>.Success(new CitySunResponse { SunData = sunData });
    }
}

/// <summary>
/// Response containing sunrise/sunset data for a city.
/// </summary>
public class CitySunResponse
{
    /// <summary>Gets or sets the sun data for each forecast day.</summary>
    public List<SunDataModel> SunData { get; set; } = [];
}

/// <summary>
/// DTO representing sunrise/sunset data for a single day.
/// </summary>
public class SunDataModel
{
    /// <summary>Gets or sets the forecast date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the sunrise time.</summary>
    public DateTime Sunrise { get; set; }

    /// <summary>Gets or sets the sunset time.</summary>
    public DateTime Sunset { get; set; }

    /// <summary>Gets or sets the daylight duration in seconds.</summary>
    public int DaylightDurationSeconds { get; set; }

    /// <summary>Gets or sets a value indicating whether it is currently daytime.</summary>
    public bool IsDay { get; set; }
}
