// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;

/// <summary>
/// DTO representing a daily weather forecast with hourly breakdown.
/// </summary>
public class WeatherForecastModel
{
    /// <summary>Gets or sets the forecast record identifier.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the city identifier.</summary>
    public string CityId { get; set; }

    /// <summary>Gets or sets the forecast date.</summary>
    public DateOnly ForecastDate { get; set; }

    /// <summary>Gets or sets the daytime weather condition code.</summary>
    public int DayWeatherCode { get; set; }

    /// <summary>Gets or sets the daytime weather description.</summary>
    public string DayWeatherDescription { get; set; }

    /// <summary>Gets or sets the daytime weather icon.</summary>
    public string DayWeatherIcon { get; set; }

    /// <summary>Gets or sets the maximum temperature.</summary>
    public decimal TemperatureMax { get; set; }

    /// <summary>Gets or sets the minimum temperature.</summary>
    public decimal TemperatureMin { get; set; }

    /// <summary>Gets or sets the maximum apparent temperature.</summary>
    public decimal ApparentTemperatureMax { get; set; }

    /// <summary>Gets or sets the minimum apparent temperature.</summary>
    public decimal ApparentTemperatureMin { get; set; }

    /// <summary>Gets or sets the total precipitation sum.</summary>
    public decimal PrecipitationSum { get; set; }

    /// <summary>Gets or sets the maximum precipitation probability.</summary>
    public int PrecipitationProbabilityMax { get; set; }

    /// <summary>Gets or sets the maximum wind speed.</summary>
    public decimal WindSpeedMax { get; set; }

    /// <summary>Gets or sets the maximum wind gusts.</summary>
    public decimal WindGustsMax { get; set; }

    /// <summary>Gets or sets the dominant wind direction in degrees.</summary>
    public int DominantWindDirection { get; set; }

    /// <summary>Gets or sets the maximum UV index.</summary>
    public decimal UvIndexMax { get; set; }

    /// <summary>Gets or sets the sunshine duration in seconds.</summary>
    public int SunshineDurationSeconds { get; set; }

    /// <summary>Gets or sets the daylight duration in seconds.</summary>
    public int DaylightDurationSeconds { get; set; }

    /// <summary>Gets or sets the sunrise time.</summary>
    public DateTime Sunrise { get; set; }

    /// <summary>Gets or sets the sunset time.</summary>
    public DateTime Sunset { get; set; }

    /// <summary>Gets or sets the ISO interval for the daylight period.</summary>
    public string DaylightPeriod { get; set; }

    /// <summary>Gets or sets human-readable daylight duration text.</summary>
    public string DaylightDurationText { get; set; }

    /// <summary>Gets or sets the hourly forecast breakdown.</summary>
    public List<HourlyForecastModel> HourlyForecasts { get; set; } = [];

    /// <summary>Gets or sets when the forecast data was retrieved.</summary>
    public DateTime RetrievedAt { get; set; }

    /// <summary>Gets or sets human-readable text describing when the forecast data was retrieved.</summary>
    public string LastUpdatedText { get; set; }

    /// <summary>Gets or sets a value indicating whether the data may be stale.</summary>
    public bool StaleDataWarning { get; set; }

    /// <summary>Gets or sets the stale data warning message.</summary>
    public string StaleDataWarningMessage { get; set; }

}
