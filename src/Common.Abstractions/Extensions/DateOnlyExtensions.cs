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
        return source.IsInRelativeRange(today, unit, amount, direction, inclusive);
    }

    /// <summary>
    /// Determines whether a date is inside a relative range around an explicit reference date.
    /// </summary>
    /// <param name="source">The date to evaluate.</param>
    /// <param name="reference">The reference date.</param>
    /// <param name="unit">The relative unit.</param>
    /// <param name="amount">The amount of units.</param>
    /// <param name="direction">The direction from the reference date.</param>
    /// <param name="inclusive">Whether the boundaries are included.</param>
    /// <returns><c>true</c> when the date is inside the range; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// <example>
    /// <code>
    /// var due = today.AddDays(3).IsInRelativeRange(today, DateUnit.Day, 5, DateTimeDirection.Future);
    /// </code>
    /// </example>
    /// </remarks>
    [DebuggerStepThrough]
    public static bool IsInRelativeRange(this DateOnly source, DateOnly reference, DateUnit unit, int amount, DateTimeDirection direction, bool inclusive = true)
    {
        var referenceDate = direction == DateTimeDirection.Past
            ? reference.Add(unit, -amount)
            : reference.Add(unit, amount);

        return direction == DateTimeDirection.Past
            ? (inclusive ? source <= reference && source >= referenceDate : source < reference && source > referenceDate)
            : (inclusive ? source >= reference && source <= referenceDate : source > reference && source < referenceDate);
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
        return date.DaysUntil(DateOnly.FromDateTime(DateTime.Now));
    }

    /// <summary>
    /// Calculates whole days from an explicit reference date to a target date.
    /// </summary>
    /// <param name="date">The target date.</param>
    /// <param name="reference">The reference date.</param>
    /// <returns>The day difference.</returns>
    /// <remarks>
    /// <example>
    /// <code>
    /// var days = dueDate.DaysUntil(today);
    /// </code>
    /// </example>
    /// </remarks>
    [DebuggerStepThrough]
    public static int DaysUntil(this DateOnly date, DateOnly reference)
    {
        return date.DayNumber - reference.DayNumber;
    }

    [DebuggerStepThrough]
    public static long ToUnixTimeSeconds(this DateOnly source)
    {
        return source.AtStartOfDay().ToUnixTimeSeconds();
    }

    /// <summary>
    /// Converts a date at midnight UTC to Unix epoch milliseconds.
    /// </summary>
    /// <param name="source">The date to convert.</param>
    /// <returns>The number of milliseconds since 1970-01-01T00:00:00Z.</returns>
    /// <remarks>
    /// <para>The date is interpreted as the start of day at offset +00:00 by default.</para>
    /// <example>
    /// <code>
    /// var timestamp = new DateOnly(2026, 1, 1).ToUnixTimeMilliseconds();
    /// </code>
    /// </example>
    /// </remarks>
    [DebuggerStepThrough]
    public static long ToUnixTimeMilliseconds(this DateOnly source)
    {
        return source.AtStartOfDay().ToUnixTimeMilliseconds();
    }

    [DebuggerStepThrough]
    public static DateTimeOffset ToDateTimeOffset(this DateOnly source, TimeSpan? offset = null)
    {
        return source.AtStartOfDay(offset);
    }

    /// <summary>
    /// Combines a date with midnight using an explicit offset.
    /// </summary>
    /// <param name="source">The date to combine.</param>
    /// <param name="offset">The offset to apply, or +00:00 when omitted.</param>
    /// <returns>A <see cref="DateTimeOffset"/> at the start of the date.</returns>
    /// <remarks>
    /// <example>
    /// <code>
    /// var instant = date.AtStartOfDay(TimeSpan.FromHours(2));
    /// </code>
    /// </example>
    /// </remarks>
    [DebuggerStepThrough]
    public static DateTimeOffset AtStartOfDay(this DateOnly source, TimeSpan? offset = null)
    {
        return source.AtTime(TimeOnly.MinValue, offset);
    }

    /// <summary>
    /// Combines a date with a time using an explicit offset.
    /// </summary>
    /// <param name="source">The date to combine.</param>
    /// <param name="time">The time to combine.</param>
    /// <param name="offset">The offset to apply, or +00:00 when omitted.</param>
    /// <returns>A <see cref="DateTimeOffset"/> for the combined wall-clock value.</returns>
    /// <remarks>
    /// <example>
    /// <code>
    /// var instant = date.AtTime(new TimeOnly(13, 45), TimeSpan.FromHours(1));
    /// </code>
    /// </example>
    /// </remarks>
    [DebuggerStepThrough]
    public static DateTimeOffset AtTime(this DateOnly source, TimeOnly time, TimeSpan? offset = null)
    {
        return new DateTimeOffset(source.ToDateTime(time), offset ?? TimeSpan.Zero);
    }

    [DebuggerStepThrough]
    public static TimeSpan TimeSpanTo(this DateOnly source, DateOnly target)
    {
        return target.ToDateTime(TimeOnly.MinValue) - source.ToDateTime(TimeOnly.MinValue);
    }

    [DebuggerStepThrough]
    public static DateOnly FloorTo(this DateOnly source, DateUnit dateUnit)
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

    [DebuggerStepThrough]
    public static DateOnly CeilingTo(this DateOnly source, DateUnit dateUnit)
    {
        var floor = source.FloorTo(dateUnit);
        if (floor == source)
        {
            return source;
        }

        return dateUnit switch
        {
            DateUnit.Day => floor.AddDays(1),
            DateUnit.Week => floor.AddDays(7),
            DateUnit.Month => floor.AddMonths(1),
            DateUnit.Year => floor.AddYears(1),
            _ => throw new ArgumentException("Unsupported DateUnit.", nameof(dateUnit))
        };
    }

    [DebuggerStepThrough]
    public static DateOnly RoundToNearest(this DateOnly source, DateUnit dateUnit)
    {
        return source.FloorTo(dateUnit);
    }

    /// <summary>
    /// Formats a date using ISO yyyy-MM-dd format.
    /// </summary>
    /// <param name="source">The date to format.</param>
    /// <returns>The invariant ISO date string.</returns>
    /// <remarks>
    /// <example>
    /// <code>
    /// var text = new DateOnly(2026, 6, 29).ToIsoDateString();
    /// </code>
    /// </example>
    /// </remarks>
    [DebuggerStepThrough]
    public static string ToIsoDateString(this DateOnly source)
    {
        return source.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
