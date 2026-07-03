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

    public CitySunQuery(string cityId, string range)
    {
        this.CityId = cityId;
        this.Range = range;
    }

    /// <summary>Gets the city identifier to retrieve sun data for.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    /// <summary>Gets the optional number of days to include.</summary>
    public int? Days { get; private set; }

    /// <summary>Gets the optional ISO date range to include.</summary>
    public string Range { get; private set; }

    [Handle]
    private async Task<Result<CitySunResponse>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        var cityId = Domain.Modules.Core.Model.CityId.Create(this.CityId);
        var periodResult = ForecastPeriod.Resolve(this.Range, this.Days ?? 1);
        if (periodResult.IsFailure)
        {
            return periodResult.Wrap<CitySunResponse>();
        }

        var period = periodResult.Value;

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
            .Where(forecast => period.Contains(forecast.ForecastDate))
            .OrderBy(forecast => forecast.ForecastDate)
            .Select(forecast =>
            {
                var daylight = forecast.DaylightPeriod;
                return new SunDataModel
                {
                    Date = forecast.ForecastDate,
                    Sunrise = forecast.Sunrise,
                    Sunset = forecast.Sunset,
                    DaylightPeriod = daylight.ToIsoRangeString(),
                    DaylightDurationSeconds = forecast.DaylightDurationSeconds,
                    DaylightDurationText = daylight.Duration.ToDurationText(new RelativeTimeFormatOptions { MinimumUnit = RelativeTimeUnit.Minute }),
                    IsDay = daylight.Contains(DateTime.UtcNow)
                };
            }).ToList();

        return Result<CitySunResponse>.Success(new CitySunResponse
        {
            Period = period.ToIsoRangeString(),
            SunData = sunData
        });
    }
}

/// <summary>
/// Response containing sunrise/sunset data for a city.
/// </summary>
public class CitySunResponse
{
    /// <summary>Gets or sets the ISO interval used to select sun data.</summary>
    public string Period { get; set; }

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

    /// <summary>Gets or sets the ISO interval for the daylight period.</summary>
    public string DaylightPeriod { get; set; }

    /// <summary>Gets or sets the daylight duration in seconds.</summary>
    public int DaylightDurationSeconds { get; set; }

    /// <summary>Gets or sets human-readable daylight duration text.</summary>
    public string DaylightDurationText { get; set; }

    /// <summary>Gets or sets a value indicating whether it is currently daytime.</summary>
    public bool IsDay { get; set; }
}
