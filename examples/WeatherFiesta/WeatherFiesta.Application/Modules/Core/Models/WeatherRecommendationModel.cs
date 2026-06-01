// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;

/// <summary>
/// DTO representing a weather-based recommendation for the user.
/// </summary>
public class WeatherRecommendationModel
{
    /// <summary>Gets or sets the recommendation category (e.g., Precipitation, UV, Temperature).</summary>
    public string Category { get; set; }

    /// <summary>Gets or sets the recommendation severity (e.g., Info, Caution, Warning).</summary>
    public string Severity { get; set; }

    /// <summary>Gets or sets the recommendation title.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the detailed recommendation message.</summary>
    public string Message { get; set; }
}
