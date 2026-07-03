// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

using System.Text.Json;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Builds prompts for weather report text generation.
/// </summary>
public static class WeatherReportPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    /// <summary>Builds the system and user prompt.</summary>
    public static WeatherReportPrompt Build(WeatherReportTextGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = new
        {
            instruction = GetInstruction(request.ReportType),
            cityName = request.CityName,
            reportType = request.ReportType.ToString(),
            periodStartUtc = request.PeriodStartUtc,
            periodEndUtc = request.PeriodEndUtc,
            forecastDateStart = request.ForecastDateStart,
            forecastDateEndExclusive = request.ForecastDateEndExclusive,
            currentWeather = request.CurrentWeather,
            days = request.Days,
            facts = CreateFacts(request)
        };

        return new WeatherReportPrompt(
            "You write factual weather reports from structured weather data. Use only the supplied data. Do not invent forecasts, warnings, alerts, causes, or trends. Do not mention missing values. Use Celsius, km/h, mm, hPa, and percentages. Return plain text only.",
            JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static string GetInstruction(WeatherReportType type)
    {
        return type switch
        {
            WeatherReportType.Today =>
                "Write a concise report for today in 1-2 sentences. Mention temperature, rain, wind, and cloud cover only when supported by the supplied data.  Warm means temperatures from 20°C and above, cool means temperatures from 10°C to 19°C, cold means temperatures below 10°C.",
            WeatherReportType.Tomorrow =>
                "Write a concise forecast for tomorrow in 1-2 sentences. Mention expected temperature range, rain risk, and wind only when supported by the supplied data.  Warm means temperatures from 20°C and above, cool means temperatures from 10°C to 19°C, cold means temperatures below 10°C.",
            WeatherReportType.Week =>
                "Write a concise 7-day outlook in 2-3 sentences. Summarize the overall trend, warmest/coolest days, rain risk, and wind only when supported by the supplied data.  Warm means temperatures from 20°C and above, cool means temperatures from 10°C to 19°C, cold means temperatures below 10°C.",
            WeatherReportType.NextBusinessDay =>
                "Write a concise forecast for the next business day in 1-2 sentences. Mention expected temperature range, rain risk, and wind only when supported by the supplied data. Warm means temperatures from 20°C and above, cool means temperatures from 10°C to 19°C, cold means temperatures below 10°C.",
            WeatherReportType.Current =>
                "Write one short current weather summary sentence. Warm means temperatures from 20°C and above, cool means temperatures from 10°C to 19°C, cold means temperatures below 10°C.",
            _ => "Write one short factual weather summary."
        };
    }

    private static object CreateFacts(WeatherReportTextGenerationRequest request)
    {
        var days = request.Days?.ToList() ?? [];
        if (days.Count == 0)
        {
            return new { };
        }

        return new
        {
            warmestDay = days.MaxBy(d => d.TemperatureMax)?.ForecastDate,
            coolestDay = days.MinBy(d => d.TemperatureMin)?.ForecastDate,
            highestRainProbabilityDay = days.MaxBy(d => d.PrecipitationProbabilityMax)?.ForecastDate,
            highestPrecipitationDay = days.MaxBy(d => d.PrecipitationSum)?.ForecastDate,
            strongestGustDay = days.MaxBy(d => d.WindGustsMax)?.ForecastDate,
            highestUvDay = days.MaxBy(d => d.UvIndexMax)?.ForecastDate,
            averagePrecipitationProbability = days.Average(d => d.PrecipitationProbabilityMax),
            totalPrecipitation = days.Sum(d => d.PrecipitationSum)
        };
    }
}

/// <summary>
/// Prompt parts for weather report generation.
/// </summary>
public sealed record WeatherReportPrompt(string System, string User);
