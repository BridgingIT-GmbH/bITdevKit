// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Shared context for the weather ingestion pipeline.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WeatherIngestionContext" /> class.
/// </remarks>
/// <param name="city">The city whose weather data is ingested.</param>
public sealed class WeatherIngestionContext(City city) : PipelineContextBase
{
    /// <summary>Gets the city whose weather data is ingested.</summary>
    public City City { get; } = city ?? throw new ArgumentNullException(nameof(city));

    /// <summary>Gets or sets the data fetched by the ingestion step.</summary>
    public WeatherIngestionResult Data { get; set; }

    /// <summary>Gets or sets the city-local period for today's generated report.</summary>
    public WeatherReportPeriod TodayReportPeriod { get; set; }

    /// <summary>Gets or sets the city-local period for tomorrow's generated report.</summary>
    public WeatherReportPeriod TomorrowReportPeriod { get; set; }

    /// <summary>Gets or sets the city-local period for the weekly generated report.</summary>
    public WeatherReportPeriod WeekReportPeriod { get; set; }
}