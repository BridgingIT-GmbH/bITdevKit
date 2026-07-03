// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using Microsoft.Extensions.Logging;

/// <summary>
/// Resolves city-local report periods for weather report generation.
/// </summary>
public sealed class ResolveWeatherReportPeriodsStep(
    ILogger<ResolveWeatherReportPeriodsStep> logger,
    TimeProvider timeProvider = null,
    IBusinessCalendarResolver businessCalendarResolver = null) : PipelineStep<WeatherIngestionContext>
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly IBusinessCalendarResolver businessCalendarResolver = businessCalendarResolver;

    /// <inheritdoc />
    protected override PipelineControl Execute(
        WeatherIngestionContext context,
        Result result,
        PipelineExecutionOptions options)
    {
        var city = context.City;
        if (string.IsNullOrWhiteSpace(city.TimeZone))
        {
            logger.LogWarning("weather report generation skipped for city {CityId} ({CityName}): timezone is missing", city.Id, city.Name);
            return PipelineControl.Break(result.WithMessage("Weather report generation skipped because timezone is missing."));
        }

        try
        {
            var utcNow = this.timeProvider.GetUtcNow().UtcDateTime;
            var localToday = GetLocalDate(utcNow, city.TimeZone);

            context.TodayReportPeriod = CreatePeriod(localToday, localToday.AddDays(1), city.TimeZone);
            context.TomorrowReportPeriod = CreatePeriod(localToday.AddDays(1), localToday.AddDays(2), city.TimeZone);
            context.WeekReportPeriod = CreatePeriod(localToday, localToday.AddDays(7), city.TimeZone);
            var calendar = this.businessCalendarResolver?.Resolve(city.CountryCode) ?? BusinessCalendars.Resolve(city.CountryCode);
            var nextBusinessDay = calendar.NextBusinessDay(localToday);
            context.NextBusinessDayReportPeriod = CreatePeriod(nextBusinessDay, nextBusinessDay.AddDays(1), city.TimeZone);

            return PipelineControl.Continue(result.WithMessage($"Weather report periods resolved for city {city.Id}."));
        }
        catch (TimeZoneNotFoundException ex)
        {
            logger.LogWarning(ex, "weather report generation skipped for city {CityId} ({CityName}): timezone {TimeZoneId} was not found", city.Id, city.Name, city.TimeZone);
            return PipelineControl.Break(result.WithMessage("Weather report generation skipped because timezone was not found."));
        }
        catch (InvalidTimeZoneException ex)
        {
            logger.LogWarning(ex, "weather report generation skipped for city {CityId} ({CityName}): timezone {TimeZoneId} is invalid", city.Id, city.Name, city.TimeZone);
            return PipelineControl.Break(result.WithMessage("Weather report generation skipped because timezone is invalid."));
        }
    }

    private static WeatherReportPeriod CreatePeriod(DateOnly localStart, DateOnly localEndExclusive, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localStartDateTime = localStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEndDateTime = localEndExclusive.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        return new WeatherReportPeriod(
            TimeZoneInfo.ConvertTimeToUtc(localStartDateTime, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(localEndDateTime, timeZone),
            localStart,
            localEndExclusive,
            timeZoneId);
    }

    private static DateOnly GetLocalDate(DateTime utcNow, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var utc = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);

        return DateOnly.FromDateTime(local);
    }
}
