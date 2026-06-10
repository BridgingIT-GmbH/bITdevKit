// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Queue message requesting generated report text for a city and report period.
/// </summary>
public sealed class WeatherReportGenerationMessage : QueueMessageBase
{
    /// <summary>Initializes a new instance of the <see cref="WeatherReportGenerationMessage" /> class.</summary>
    public WeatherReportGenerationMessage()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WeatherReportGenerationMessage" /> class.</summary>
    public WeatherReportGenerationMessage(
        CityId cityId,
        WeatherReportType reportType,
        WeatherReportPeriod period)
    {
        this.CityId = cityId.Value.ToString("D");
        this.ReportType = reportType;
        this.PeriodStartUtc = EnsureUtc(period.PeriodStartUtc);
        this.PeriodEndUtc = EnsureUtc(period.PeriodEndUtc);
        this.ForecastDateStart = period.ForecastDateStart;
        this.ForecastDateEndExclusive = period.ForecastDateEndExclusive;
        this.TimeZoneId = period.TimeZoneId;
    }

    /// <summary>Gets or sets the city identifier.</summary>
    public string CityId { get; set; }

    /// <summary>Gets or sets the report type.</summary>
    public WeatherReportType ReportType { get; set; }

    /// <summary>Gets or sets the UTC start of the report period.</summary>
    public DateTime PeriodStartUtc { get; set; }

    /// <summary>Gets or sets the UTC end of the report period.</summary>
    public DateTime PeriodEndUtc { get; set; }

    /// <summary>Gets or sets the city-local forecast date at which the report starts.</summary>
    public DateOnly ForecastDateStart { get; set; }

    /// <summary>Gets or sets the exclusive city-local forecast date at which the report ends.</summary>
    public DateOnly ForecastDateEndExclusive { get; set; }

    /// <summary>Gets or sets the timezone identifier used for the report semantics.</summary>
    public string TimeZoneId { get; set; }

    /// <summary>Gets the typed city identifier.</summary>
    public CityId GetCityId() => Domain.Modules.Core.Model.CityId.Create(Guid.Parse(this.CityId));

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value
        };
    }
}
