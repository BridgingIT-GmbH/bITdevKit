// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Defines the type of generated weather report.
/// </summary>
public enum WeatherReportType
{
    /// <summary>Current conditions report based mainly on the latest current weather.</summary>
    Current = 0,

    /// <summary>Today report based on the city-local forecast day.</summary>
    Today = 1,

    /// <summary>Tomorrow report based on the next city-local forecast day.</summary>
    Tomorrow = 2,

    /// <summary>Seven-day outlook based on the available city-local forecast days.</summary>
    Week = 3,

    /// <summary>Forecast report for the next city-local business day.</summary>
    NextBusinessDay = 4
}
