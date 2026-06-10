// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Generates weather report text from compact structured weather input.
/// </summary>
public interface IWeatherReportTextGenerator
{
    /// <summary>Generates a weather report summary.</summary>
    Task<Result<string>> GenerateAsync(
        WeatherReportTextGenerationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compact structured input for weather report text generation.
/// </summary>
public sealed record WeatherReportTextGenerationRequest(
    string CityName,
    WeatherReportType ReportType,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    DateOnly ForecastDateStart,
    DateOnly ForecastDateEndExclusive,
    IReadOnlyCollection<WeatherForecastDayInput> Days,
    CurrentWeatherInput CurrentWeather);

/// <summary>
/// Current weather input for report generation.
/// </summary>
public sealed record CurrentWeatherInput(
    decimal Temperature,
    decimal ApparentTemperature,
    int Humidity,
    int WeatherCode,
    decimal WindSpeed,
    int WindDirection,
    decimal WindGusts,
    decimal Precipitation,
    int CloudCover,
    decimal Pressure,
    DateTime RetrievedAt);

/// <summary>
/// Daily forecast input for report generation.
/// </summary>
public sealed record WeatherForecastDayInput(
    DateOnly ForecastDate,
    int WeatherCode,
    decimal TemperatureMin,
    decimal TemperatureMax,
    decimal ApparentTemperatureMin,
    decimal ApparentTemperatureMax,
    decimal PrecipitationSum,
    int PrecipitationProbabilityMax,
    decimal WindSpeedMax,
    decimal WindGustsMax,
    int DominantWindDirection,
    decimal UvIndexMax,
    int SunshineDurationSeconds,
    int DaylightDurationSeconds,
    IReadOnlyCollection<WeatherForecastHourHighlightInput> HourHighlights);

/// <summary>
/// Selected hourly forecast highlight input for report generation.
/// </summary>
public sealed record WeatherForecastHourHighlightInput(
    int Hour,
    decimal Temperature,
    decimal ApparentTemperature,
    int PrecipitationProbability,
    decimal Precipitation,
    int WeatherCode,
    decimal WindSpeed,
    decimal WindGusts,
    int CloudCover,
    bool IsDay);
