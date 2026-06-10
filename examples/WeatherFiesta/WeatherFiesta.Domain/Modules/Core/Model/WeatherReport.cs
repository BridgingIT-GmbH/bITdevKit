// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Represents a generated textual weather report for a city and report period.
/// </summary>
[DebuggerDisplay("Id={Id}, CityId={CityId}, Type={ReportType}, Start={PeriodStartUtc}")]
[TypedEntityId<Guid>]
public class WeatherReport : ActiveEntity<WeatherReport, WeatherReportId>
{
    /// <summary>Gets or sets the city identifier.</summary>
    public CityId CityId { get; set; }

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

    /// <summary>Gets or sets the generated report summary.</summary>
    public string Summary { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the report was generated.</summary>
    public DateTime GeneratedAt { get; set; }

    private WeatherReport()
    {
    }

    /// <summary>
    /// Creates a new weather report shell for the specified city and period.
    /// </summary>
    public static WeatherReport Create(
        CityId cityId,
        WeatherReportType reportType,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateOnly forecastDateStart,
        DateOnly forecastDateEndExclusive)
    {
        return new WeatherReport
        {
            CityId = cityId,
            ReportType = reportType,
            PeriodStartUtc = EnsureUtc(periodStartUtc),
            PeriodEndUtc = EnsureUtc(periodEndUtc),
            ForecastDateStart = forecastDateStart,
            ForecastDateEndExclusive = forecastDateEndExclusive,
            Summary = string.Empty,
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Sets the generated report content.
    /// </summary>
    public void SetContent(string summary)
    {
        this.Summary = summary;
        this.GeneratedAt = DateTime.UtcNow;
    }

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
