// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Represents a weather alert computed at query time by the WeatherRuleEngine.
/// Not persisted — computed from current weather data.
/// </summary>
public class WeatherAlert
{
    /// <summary>Gets or sets the alert type (e.g., Thunderstorm, SevereWind).</summary>
    public AlertType Type { get; set; }

    /// <summary>Gets or sets the alert severity (e.g., Warning, Severe, Extreme).</summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>Gets or sets the human-readable alert message.</summary>
    public string Message { get; set; }

    /// <summary>Gets or sets the associated WMO weather condition code.</summary>
    public int? WeatherCode { get; set; }

    /// <summary>Gets or sets the wind speed that triggered the alert, if applicable.</summary>
    public decimal? WindSpeed { get; set; }

    /// <summary>Gets or sets the temperature that triggered the alert, if applicable.</summary>
    public decimal? Temperature { get; set; }
}
