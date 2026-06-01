// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Evaluates weather alert and recommendation rules at query time.
/// All rules are defined per ADR-0005 and PRD-0600.
/// </summary>
public static class WeatherRuleEngine
{
    // Alert thresholds
    private const decimal SevereWindThresholdKmh = 80m;
    private const decimal ExtremeHeatThresholdC = 40m;
    private const decimal BlizzardWindThresholdKmh = 50m;
    private const decimal HurricaneWindThresholdKmh = 118m;

    // Blizzard WMO codes: 71, 73, 75, 77 (snow), 56, 57, 66, 67 (freezing drizzle/rain)
    private static readonly HashSet<int> BlizzardWeatherCodes = [71, 73, 75, 77, 56, 57, 66, 67];

    // Thunderstorm WMO codes: 95, 96, 99
    private static readonly HashSet<int> ThunderstormWeatherCodes = [95, 96, 99];

    // Hail WMO codes: 96, 99
    private static readonly HashSet<int> HailWeatherCodes = [96, 99];

    /// <summary>
    /// Evaluates weather alert rules for the given current weather data.
    /// Returns alerts sorted by severity (Extreme > Severe > Warning).
    /// </summary>
    public static List<WeatherAlert> EvaluateAlerts(int weatherCode, decimal windSpeed, decimal temperature)
    {
        var alerts = new List<WeatherAlert>();

        // Thunderstorm: WMO codes 95, 96, 99 → Warning
        if (ThunderstormWeatherCodes.Contains(weatherCode))
        {
            alerts.Add(new WeatherAlert
            {
                Type = AlertType.Thunderstorm,
                Severity = AlertSeverity.Warning,
                Message = $"Thunderstorm detected (WMO code {weatherCode})",
                WeatherCode = weatherCode
            });
        }

        // Hail: WMO codes 96, 99 → Warning
        if (HailWeatherCodes.Contains(weatherCode))
        {
            alerts.Add(new WeatherAlert
            {
                Type = AlertType.Hail,
                Severity = AlertSeverity.Warning,
                Message = $"Hail detected (WMO code {weatherCode})",
                WeatherCode = weatherCode
            });
        }

        // Severe wind: >80 km/h → Severe
        if (windSpeed > SevereWindThresholdKmh)
        {
            alerts.Add(new WeatherAlert
            {
                Type = AlertType.SevereWind,
                Severity = AlertSeverity.Severe,
                Message = $"Severe wind speed: {windSpeed:F1} km/h (threshold: {SevereWindThresholdKmh} km/h)",
                WindSpeed = windSpeed
            });
        }

        // Extreme heat: >40°C → Warning
        if (temperature > ExtremeHeatThresholdC)
        {
            alerts.Add(new WeatherAlert
            {
                Type = AlertType.ExtremeHeat,
                Severity = AlertSeverity.Warning,
                Message = $"Extreme heat: {temperature:F1}°C (threshold: {ExtremeHeatThresholdC}°C)",
                Temperature = temperature
            });
        }

        // Blizzard: WMO 71-77,56,57,66,67 + wind >50 km/h → Warning
        if (BlizzardWeatherCodes.Contains(weatherCode) && windSpeed > BlizzardWindThresholdKmh)
        {
            alerts.Add(new WeatherAlert
            {
                Type = AlertType.Blizzard,
                Severity = AlertSeverity.Warning,
                Message = $"Blizzard conditions: WMO code {weatherCode} with wind {windSpeed:F1} km/h",
                WeatherCode = weatherCode,
                WindSpeed = windSpeed
            });
        }

        // Hurricane: wind >118 km/h → Extreme
        if (windSpeed > HurricaneWindThresholdKmh)
        {
            alerts.Add(new WeatherAlert
            {
                Type = AlertType.Hurricane,
                Severity = AlertSeverity.Extreme,
                Message = $"Hurricane force winds: {windSpeed:F1} km/h (threshold: {HurricaneWindThresholdKmh} km/h)",
                WindSpeed = windSpeed
            });
        }

        return alerts.OrderByDescending(a => a.Severity.Id).ToList();
    }

    /// <summary>
    /// Evaluates weather recommendation rules for the given weather data.
    /// Returns recommendations sorted by severity (Warning > Caution > Info), then by category.
    /// </summary>
    public static List<WeatherRecommendation> EvaluateRecommendations(
        int weatherCode,
        decimal temperature,
        decimal windSpeed,
        int humidity,
        decimal precipitation,
        int precipitationProbability,
        decimal uvIndex)
    {
        var recommendations = new List<WeatherRecommendation>();

        // Rule 1: Rain expected (precipitation probability > 60%) → Precipitation/Caution
        if (precipitationProbability > 60)
        {
            recommendations.Add(new WeatherRecommendation
            {
                Category = RecommendationCategory.Precipitation,
                Severity = RecommendationSeverity.Caution,
                Title = "Rain expected",
                Message = $"Precipitation probability is {precipitationProbability}%. Consider bringing an umbrella."
            });
        }

        // Rule 2: High UV index (>6) → UV/Warning
        if (uvIndex > 6)
        {
            recommendations.Add(new WeatherRecommendation
            {
                Category = RecommendationCategory.UV,
                Severity = RecommendationSeverity.Warning,
                Title = "High UV index",
                Message = $"UV index is {uvIndex:F1}. Apply sunscreen and wear protective clothing."
            });
        }
        else if (uvIndex > 3)
        {
            recommendations.Add(new WeatherRecommendation
            {
                Category = RecommendationCategory.UV,
                Severity = RecommendationSeverity.Info,
                Title = "Moderate UV index",
                Message = $"UV index is {uvIndex:F1}. Consider sunscreen if outdoors."
            });
        }

        // Rule 3: Extreme cold (<-10°C) → Temperature/Warning
        if (temperature < -10)
        {
            recommendations.Add(new WeatherRecommendation
            {
                Category = RecommendationCategory.Temperature,
                Severity = RecommendationSeverity.Warning,
                Title = "Extreme cold",
                Message = $"Temperature is {temperature:F1}°C. Limit outdoor exposure and dress warmly."
            });
        }
        else if (temperature < 0)
        {
            recommendations.Add(new WeatherRecommendation
            {
                Category = RecommendationCategory.Temperature,
                Severity = RecommendationSeverity.Caution,
                Title = "Freezing temperatures",
                Message = $"Temperature is {temperature:F1}°C. Watch for icy conditions."
            });
        }

        // Rule 4: Strong wind (>50 km/h) → Wind/Caution
        if (windSpeed > 50)
        {
            recommendations.Add(new WeatherRecommendation
            {
                Category = RecommendationCategory.Wind,
                Severity = RecommendationSeverity.Caution,
                Title = "Strong wind",
                Message = $"Wind speed is {windSpeed:F1} km/h. Be cautious outdoors."
            });
        }

        // Rule 5: Thunderstorm (WMO 95,96,99) → Storm/Warning
        if (ThunderstormWeatherCodes.Contains(weatherCode))
        {
            recommendations.Add(new WeatherRecommendation
            {
                Category = RecommendationCategory.Storm,
                Severity = RecommendationSeverity.Warning,
                Title = "Thunderstorm activity",
                Message = "Thunderstorm detected. Seek shelter and avoid open areas."
            });
        }

        // Rule 6: High humidity (>80%) + warm (>25°C) → General/Info
        if (humidity > 80 && temperature > 25)
        {
            recommendations.Add(new WeatherRecommendation
            {
                Category = RecommendationCategory.General,
                Severity = RecommendationSeverity.Info,
                Title = "High humidity",
                Message = $"Humidity is {humidity}% with temperature {temperature:F1}°C. Stay hydrated."
            });
        }

        // Rule 7: Snow (WMO 71-77) → Precipitation/Caution
        if (weatherCode is 71 or 73 or 75 or 77)
        {
            recommendations.Add(new WeatherRecommendation
            {
                Category = RecommendationCategory.Precipitation,
                Severity = RecommendationSeverity.Caution,
                Title = "Snowfall",
                Message = "Snow detected. Watch for slippery surfaces and reduced visibility."
            });
        }

        return recommendations
            .OrderByDescending(r => r.Severity.Id)
            .ThenBy(r => r.Category.Id)
            .ToList();
    }
}
