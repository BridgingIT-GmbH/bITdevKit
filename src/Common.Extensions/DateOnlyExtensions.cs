// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

public static class DateOnlyExtensions
{
    /// <summary>
    /// Gets the start of the day for the specified DateOnly.
    /// </summary>
    /// <param name="source">The DateOnly to get the start of the day for.</param>
    /// <returns>A DateOnly representing the start of the day.</returns>
    [DebuggerStepThrough]
    public static DateOnly StartOfDay(this DateOnly source)
    {
        return source; // DateOnly already represents the whole day without time.
    }

    /// <summary>
    /// Gets the end of the day for the specified DateOnly.
    /// </summary>
    /// <param name="source">The DateOnly to get the end of the day for.</param>
    /// <returns>A DateOnly representing the end of the day.</returns>
    [DebuggerStepThrough]
    public static DateOnly EndOfDay(this DateOnly source)
    {
        return source; // Same as StartOfDay since DateOnly has no time component.
    }

    /// <summary>
    /// Gets the start of the week for the specified DateOnly.
    /// </summary>
    /// <param name="source">The DateOnly to get the start of the week for.</param>
    /// <param name="day"></param>
    /// <returns>A DateOnly representing the start of the week.</returns>
    [DebuggerStepThrough]
    public static DateOnly StartOfWeek(this DateOnly source, DayOfWeek day = DayOfWeek.Monday)
    {
        var offset = source.DayOfWeek - day;
        if (offset < 0)
        {
            offset += 7;
        }

        return source.AddDays(-1 * offset).StartOfDay();
    }

    /// <summary>
    /// Gets the end of the week for the specified DateOnly.
    /// </summary>
    /// <param name="date">The DateOnly to get the end of the week for.</param>
    /// <returns>A DateOnly representing the end of the week.</returns>
    [DebuggerStepThrough]
    public static DateOnly EndOfWeek(this DateOnly date)
    {
        return date.AddDays(7 - (int)date.DayOfWeek);
    }

    /// <summary>
    /// Gets the start of the month for the specified DateOnly.
    /// </summary>
    /// <param name="date">The DateOnly to get the start of the month for.</param>
    /// <returns>A DateOnly representing the start of the month.</returns>
    [DebuggerStepThrough]
    public static DateOnly StartOfMonth(this DateOnly date)
    {
        return new DateOnly(date.Year, date.Month, 1);
    }

    /// <summary>
    /// Gets the end of the month for the specified DateOnly.
    /// </summary>
    /// <param name="source">The DateOnly to get the end of the month for.</param>
    /// <returns>A DateOnly representing the end of the month.</returns>
    [DebuggerStepThrough]
    public static DateOnly EndOfMonth(this DateOnly source)
    {
        return new DateOnly(source.Year, source.Month, DateTime.DaysInMonth(source.Year, source.Month));
    }

    /// <summary>
    /// Gets the start of the year for the specified DateOnly.
    /// </summary>
    /// <param name="source">The DateOnly to get the start of the year for.</param>
    /// <returns>A DateOnly representing the start of the year.</returns>
    [DebuggerStepThrough]
    public static DateOnly StartOfYear(this DateOnly source)
    {
        return new DateOnly(source.Year, 1, 1);
    }

    /// <summary>
    /// Gets the end of the year for the specified DateOnly.
    /// </summary>
    /// <param name="source">The DateOnly to get the end of the year for.</param>
    /// <returns>A DateOnly representing the end of the year.</returns>
    [DebuggerStepThrough]
    public static DateOnly EndOfYear(this DateOnly source)
    {
        return new DateOnly(source.Year, 12, 31);
    }

    [DebuggerStepThrough]
    public static DateOnly Add(this DateOnly source, DateUnit unit, int amount)
    {
        return unit switch
        {
            DateUnit.Day => source.AddDays(amount),
            DateUnit.Week => source.AddDays(7 * amount),
            DateUnit.Month => AddMonths(source, amount),
            DateUnit.Year => AddMonths(source, amount * 12),
            _ => throw new ArgumentException("Unsupported DateUnit.", nameof(unit))
        };
    }

    [DebuggerStepThrough]
    private static DateOnly AddMonths(DateOnly date, int months)
    {
        // Calculate the target year and month
        var totalMonths = date.Year * 12 + (date.Month - 1) + months;
        var targetYear = totalMonths / 12;
        var targetMonth = totalMonths % 12 + 1;

        // Determine the last day of the target month to avoid invalid dates
        var daysInTargetMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        var targetDay = Math.Min(date.Day, daysInTargetMonth);

        return new DateOnly(targetYear, targetMonth, targetDay);
    }

    [DebuggerStepThrough]
    public static bool IsInRange(this DateOnly source, DateOnly start, DateOnly end, bool inclusive = true)
    {
        return inclusive ? source >= start && source <= end : source > start && source < end;
    }

    [DebuggerStepThrough]
    public static bool IsInRelativeRange(this DateOnly source, DateUnit unit, int amount, DateTimeDirection direction, bool inclusive = true)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var referenceDate = direction == DateTimeDirection.Past ? today.Add(unit, -amount) : today.Add(unit, amount);

        return direction == DateTimeDirection.Past
            ? (inclusive ? source <= today && source >= referenceDate : source < today && source > referenceDate)
            : (inclusive ? source >= today && source <= referenceDate : source > today && source < referenceDate);
    }

    [DebuggerStepThrough]
    public static int GetWeekOfYear(this DateOnly source)
    {
        var cultureInfo = CultureInfo.InvariantCulture;
        var calendar = cultureInfo.Calendar;
        var dateTime = source.ToDateTime(TimeOnly.MinValue);

        return calendar.GetWeekOfYear(dateTime, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
    }

    [DebuggerStepThrough]
    public static bool IsLeapYear(this DateOnly source)
    {
        return DateTime.IsLeapYear(source.Year);
    }

    [DebuggerStepThrough]
    public static int DaysUntil(this DateOnly date)
    {
        return (date.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
    }

    [DebuggerStepThrough]
    public static long ToUnixTimeSeconds(this DateOnly source)
    {
        return new DateTimeOffset(source.ToDateTime(TimeOnly.MinValue)).ToUnixTimeSeconds();
    }

    [DebuggerStepThrough]
    public static DateTimeOffset ToDateTimeOffset(this DateOnly source, TimeSpan? offset = null)
    {
        return new DateTimeOffset(source.ToDateTime(TimeOnly.MinValue), offset ?? TimeSpan.Zero);
    }

    [DebuggerStepThrough]
    public static TimeSpan TimeSpanTo(this DateOnly source, DateOnly target)
    {
        return target.ToDateTime(TimeOnly.MinValue) - source.ToDateTime(TimeOnly.MinValue);
    }

    [DebuggerStepThrough]
    public static DateOnly RoundToNearest(this DateOnly source, DateUnit dateUnit)
    {
        switch (dateUnit)
        {
            case DateUnit.Day:
                return source; // DateOnly is already at the day level
            case DateUnit.Week:
                return source.StartOfWeek();
            case DateUnit.Month:
                return source.StartOfMonth();
            case DateUnit.Year:
                return source.StartOfYear();
            default:
                throw new ArgumentException("Unsupported DateUnit.", nameof(dateUnit));
        }
    }
}