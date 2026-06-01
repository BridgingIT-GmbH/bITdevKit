// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;

/// <summary>
/// DTO representing a weather alert based on current conditions.
/// </summary>
public class WeatherAlertModel
{
    /// <summary>Gets or sets the alert type (e.g., Thunderstorm, SevereWind).</summary>
    public string Type { get; set; }

    /// <summary>Gets or sets the alert severity (e.g., Warning, Severe, Extreme).</summary>
    public string Severity { get; set; }

    /// <summary>Gets or sets the human-readable alert message.</summary>
    public string Message { get; set; }

    /// <summary>Gets or sets the associated WMO weather code.</summary>
    public int? WeatherCode { get; set; }

    /// <summary>Gets or sets the wind speed threshold that triggered the alert.</summary>
    public decimal? WindSpeed { get; set; }

    /// <summary>Gets or sets the temperature threshold that triggered the alert.</summary>
    public decimal? Temperature { get; set; }
}
