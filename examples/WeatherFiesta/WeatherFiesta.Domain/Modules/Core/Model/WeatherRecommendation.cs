// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Represents a weather recommendation computed at query time by the WeatherRuleEngine.
/// Not persisted — computed from current/forecast weather data.
/// </summary>
public class WeatherRecommendation
{
    /// <summary>Gets or sets the recommendation category (e.g., Precipitation, UV, Temperature).</summary>
    public RecommendationCategory Category { get; set; }

    /// <summary>Gets or sets the recommendation severity (e.g., Info, Caution, Warning).</summary>
    public RecommendationSeverity Severity { get; set; }

    /// <summary>Gets or sets the short recommendation title.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the detailed recommendation message.</summary>
    public string Message { get; set; }
}
