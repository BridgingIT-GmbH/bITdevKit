// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides the default BCL-backed implementation of <see cref="IJobCalendarEngine"/>.
/// </summary>
public class DefaultJobCalendarEngine : IJobCalendarEngine
{
    /// <inheritdoc />
    public Result Validate(JobCalendarDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.HasSelectors
            ? Result.Success()
            : Result.Failure().WithError(new ValidationError("A calendar trigger requires at least one business-day, weekday, day-of-month, or explicit-date selector."));
    }

    /// <inheritdoc />
    public Result<DateTimeOffset?> GetNextOccurrenceUtc(
        JobCalendarDefinition definition,
        DateTimeOffset fromUtc,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var occurrencesResult = this.GetOccurrencesUtc(definition, fromUtc, fromUtc.AddYears(5), timeZone, false, true);
        if (!occurrencesResult.IsSuccess)
        {
            return Result<DateTimeOffset?>.Failure(default(DateTimeOffset?)).WithErrors(occurrencesResult.Errors);
        }

        return Result<DateTimeOffset?>.Success(occurrencesResult.Value.FirstOrDefault());
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<DateTimeOffset>> GetOccurrencesUtc(
        JobCalendarDefinition definition,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        TimeZoneInfo timeZone,
        bool fromInclusive = false,
        bool toInclusive = true)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var validation = this.Validate(definition);
        if (!validation.IsSuccess)
        {
            return Result<IReadOnlyList<DateTimeOffset>>.Failure([]).WithErrors(validation.Errors);
        }

        if (toUtc < fromUtc)
        {
            return Result<IReadOnlyList<DateTimeOffset>>.Success([]);
        }

        var fromLocal = TimeZoneInfo.ConvertTime(fromUtc, timeZone);
        var toLocal = TimeZoneInfo.ConvertTime(toUtc, timeZone);
        var startDate = DateOnly.FromDateTime(fromLocal.DateTime.Date);
        var endDate = DateOnly.FromDateTime(toLocal.DateTime.Date);
        var dates = new SortedSet<DateOnly>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (Matches(date, definition))
            {
                dates.Add(date);
            }
        }

        var occurrences = dates
            .Select(date => ConvertLocalToUtc(date, definition.TimeOfDay, timeZone))
            .Where(occurrenceUtc => fromInclusive ? occurrenceUtc >= fromUtc : occurrenceUtc > fromUtc)
            .Where(occurrenceUtc => toInclusive ? occurrenceUtc <= toUtc : occurrenceUtc < toUtc)
            .OrderBy(x => x)
            .ToArray();

        return Result<IReadOnlyList<DateTimeOffset>>.Success(occurrences);
    }

    private static bool Matches(DateOnly date, JobCalendarDefinition definition)
    {
        if (definition.ExcludedDates.Contains(date))
        {
            return false;
        }

        if (definition.ExplicitDates.Contains(date))
        {
            return true;
        }

        var matched = false;

        if (definition.BusinessDaysOnly)
        {
            matched = matched || date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        }

        if (definition.Weekdays.Count > 0)
        {
            matched = matched || definition.Weekdays.Contains(date.DayOfWeek);
        }

        if (definition.DaysOfMonth.Count > 0)
        {
            matched = matched || definition.DaysOfMonth.Any(day => MatchesDayOfMonth(date, day));
        }

        return matched;
    }

    private static bool MatchesDayOfMonth(DateOnly date, int configuredDay)
    {
        var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
        var effectiveDay = configuredDay > 0 ? configuredDay : daysInMonth + configuredDay + 1;

        return effectiveDay >= 1 && effectiveDay <= daysInMonth && date.Day == effectiveDay;
    }

    private static DateTimeOffset ConvertLocalToUtc(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            return timeZone.GetAmbiguousTimeOffsets(local)
                .Select(offset => new DateTimeOffset(local, offset).ToUniversalTime())
                .OrderBy(x => x)
                .First();
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }
}