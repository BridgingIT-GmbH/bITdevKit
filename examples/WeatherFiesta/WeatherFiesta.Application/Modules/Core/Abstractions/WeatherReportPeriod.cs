// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Represents a city-local weather report period with UTC persistence boundaries.
/// </summary>
public sealed record WeatherReportPeriod(
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    DateOnly ForecastDateStart,
    DateOnly ForecastDateEndExclusive,
    string TimeZoneId);