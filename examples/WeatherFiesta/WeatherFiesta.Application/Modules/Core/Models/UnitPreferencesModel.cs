// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;

/// <summary>
/// DTO representing user unit preferences for temperature and wind speed display.
/// </summary>
public class UnitPreferencesModel
{
    /// <summary>Gets or sets the temperature unit name (e.g., Celsius, Fahrenheit).</summary>
    public string TemperatureUnit { get; set; }

    /// <summary>Gets or sets the temperature symbol (e.g., °C, °F).</summary>
    public string TemperatureSymbol { get; set; }

    /// <summary>Gets or sets the wind speed unit name (e.g., Kmh, Mph).</summary>
    public string WindSpeedUnit { get; set; }

    /// <summary>Gets or sets the wind speed symbol (e.g., km/h, mph).</summary>
    public string WindSpeedSymbol { get; set; }
}
