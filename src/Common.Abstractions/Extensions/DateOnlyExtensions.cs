// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Provides calendar arithmetic, range checks, boundary calculations, and timestamp conversions for <see cref="DateOnly"/> values.
/// </summary>
public static class DateOnlyExtensions
{
    /// <param name="source">The DateOnly to get the start of the day for.</param>
    extension(DateOnly source)
    {
        /// <summary>
        /// Gets the start of the day for the specified DateOnly.
        /// </summary>
        /// <returns>A DateOnly representing the start of the day.</returns>
        [DebuggerStepThrough]
        public DateOnly StartOfDay()
        {
            return source; // DateOnly already represents the whole day without time.
        }

        /// <summary>
        /// Gets the end of the day for the specified DateOnly.
        /// </summary>
        /// <returns>A DateOnly representing the end of the day.</returns>
        [DebuggerStepThrough]
        public DateOnly EndOfDay()
        {
            return source; // Same as StartOfDay since DateOnly has no time component.
        }

        /// <summary>
        /// Gets the start of the week for the specified DateOnly.
        /// </summary>
        /// <param name="day"></param>
        /// <returns>A DateOnly representing the start of the week.</returns>
        [DebuggerStepThrough]
        public DateOnly StartOfWeek(DayOfWeek day = DayOfWeek.Monday)
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
        /// <returns>A DateOnly representing the end of the week.</returns>
        [DebuggerStepThrough]
        public DateOnly EndOfWeek()
        {
            return source.AddDays(7 - (int)source.DayOfWeek);
        }

        /// <summary>
        /// Gets the start of the month for the specified DateOnly.
        /// </summary>
        /// <returns>A DateOnly representing the start of the month.</returns>
        [DebuggerStepThrough]
        public DateOnly StartOfMonth()
        {
            return new DateOnly(source.Year, source.Month, 1);
        }

        /// <summary>
        /// Gets the end of the month for the specified DateOnly.
        /// </summary>
        /// <returns>A DateOnly representing the end of the month.</returns>
        [DebuggerStepThrough]
        public DateOnly EndOfMonth()
        {
            return new DateOnly(source.Year, source.Month, DateTime.DaysInMonth(source.Year, source.Month));
        }

        /// <summary>
        /// Gets the start of the year for the specified DateOnly.
        /// </summary>
        /// <returns>A DateOnly representing the start of the year.</returns>
        [DebuggerStepThrough]
        public DateOnly StartOfYear()
        {
            return new DateOnly(source.Year, 1, 1);
        }

        /// <summary>
        /// Gets the end of the year for the specified DateOnly.
        /// </summary>
        /// <returns>A DateOnly representing the end of the year.</returns>
        [DebuggerStepThrough]
        public DateOnly EndOfYear()
        {
            return new DateOnly(source.Year, 12, 31);
        }

        /// <summary>
        /// Adds a number of calendar days, seven-day weeks, calendar months, or calendar years to a date.
        /// </summary>
        /// <param name="unit">The calendar unit to add.</param>
        /// <param name="amount">The signed number of units to add.</param>
        /// <returns>The adjusted date, clamping the day to the last valid day when changing month or year.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="unit"/> is unsupported.</exception>
        [DebuggerStepThrough]
        public DateOnly Add(DateUnit unit, int amount)
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

    /// <param name="source">The date to evaluate.</param>
    extension(DateOnly source)
    {
        /// <summary>
        /// Determines whether a date lies between two boundaries.
        /// </summary>
        /// <param name="start">The lower boundary.</param>
        /// <param name="end">The upper boundary.</param>
        /// <param name="inclusive">Whether equality with either boundary counts as in range.</param>
        /// <returns><see langword="true"/> when the date satisfies the selected boundary comparison.</returns>
        [DebuggerStepThrough]
        public bool IsInRange(DateOnly start, DateOnly end, bool inclusive = true)
        {
            return inclusive ? source >= start && source <= end : source > start && source < end;
        }

        /// <summary>
        /// Determines whether a date is within a past or future range relative to the current local date.
        /// </summary>
        /// <param name="unit">The calendar unit used to calculate the range boundary.</param>
        /// <param name="amount">The number of units between today and the range boundary.</param>
        /// <param name="direction">Whether the range extends into the past or future.</param>
        /// <param name="inclusive">Whether today and the calculated boundary are included.</param>
        /// <returns><see langword="true"/> when the date falls in the calculated range.</returns>
        [DebuggerStepThrough]
        public bool IsInRelativeRange(DateUnit unit, int amount, DateTimeDirection direction, bool inclusive = true)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return source.IsInRelativeRange(today, unit, amount, direction, inclusive);
        }

        /// <summary>
        /// Determines whether a date is inside a relative range around an explicit reference date.
        /// </summary>
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
        public bool IsInRelativeRange(DateOnly reference, DateUnit unit, int amount, DateTimeDirection direction, bool inclusive = true)
        {
            var referenceDate = direction == DateTimeDirection.Past
                ? reference.Add(unit, -amount)
                : reference.Add(unit, amount);

            return direction == DateTimeDirection.Past
                ? (inclusive ? source <= reference && source >= referenceDate : source < reference && source > referenceDate)
                : (inclusive ? source >= reference && source <= referenceDate : source > reference && source < referenceDate);
        }

        /// <summary>
        /// Gets the invariant-culture week number using Monday as the first day and the first-day calendar rule.
        /// </summary>
        /// <returns>The calendar week number.</returns>
        [DebuggerStepThrough]
        public int GetWeekOfYear()
        {
            var cultureInfo = CultureInfo.InvariantCulture;
            var calendar = cultureInfo.Calendar;
            var dateTime = source.ToDateTime(TimeOnly.MinValue);

            return calendar.GetWeekOfYear(dateTime, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
        }

        /// <summary>
        /// Determines whether the date belongs to a leap year in the Gregorian calendar.
        /// </summary>
        /// <returns><see langword="true"/> when the year is a leap year.</returns>
        [DebuggerStepThrough]
        public bool IsLeapYear()
        {
            return DateTime.IsLeapYear(source.Year);
        }

        /// <summary>
        /// Calculates the signed number of whole days from the current local date to a target date.
        /// </summary>
        /// <returns>A positive value for a future date, zero for today, or a negative value for a past date.</returns>
        [DebuggerStepThrough]
        public int DaysUntil()
        {
            return source.DaysUntil(DateOnly.FromDateTime(DateTime.Now));
        }

        /// <summary>
        /// Calculates whole days from an explicit reference date to a target date.
        /// </summary>
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
        public int DaysUntil(DateOnly reference)
        {
            return source.DayNumber - reference.DayNumber;
        }

        /// <summary>
        /// Converts midnight UTC on a date to Unix epoch seconds.
        /// </summary>
        /// <returns>The number of seconds since 1970-01-01T00:00:00Z.</returns>
        [DebuggerStepThrough]
        public long ToUnixTimeSeconds()
        {
            return source.AtStartOfDay().ToUnixTimeSeconds();
        }

        /// <summary>
        /// Converts a date at midnight UTC to Unix epoch milliseconds.
        /// </summary>
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
        public long ToUnixTimeMilliseconds()
        {
            return source.AtStartOfDay().ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Combines a date with midnight using a specified offset.
        /// </summary>
        /// <param name="offset">The offset to apply, or UTC when omitted.</param>
        /// <returns>A date-time offset at the start of the date.</returns>
        [DebuggerStepThrough]
        public DateTimeOffset ToDateTimeOffset(TimeSpan? offset = null)
        {
            return source.AtStartOfDay(offset);
        }

        /// <summary>
        /// Combines a date with midnight using an explicit offset.
        /// </summary>
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
        public DateTimeOffset AtStartOfDay(TimeSpan? offset = null)
        {
            return source.AtTime(TimeOnly.MinValue, offset);
        }

        /// <summary>
        /// Combines a date with a time using an explicit offset.
        /// </summary>
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
        public DateTimeOffset AtTime(TimeOnly time, TimeSpan? offset = null)
        {
            return new DateTimeOffset(source.ToDateTime(time), offset ?? TimeSpan.Zero);
        }

        /// <summary>
        /// Calculates the signed midnight-to-midnight duration from one date to another.
        /// </summary>
        /// <param name="target">The ending date.</param>
        /// <returns>The whole-day duration from <paramref name="source"/> to <paramref name="target"/>.</returns>
        [DebuggerStepThrough]
        public TimeSpan TimeSpanTo(DateOnly target)
        {
            return target.ToDateTime(TimeOnly.MinValue) - source.ToDateTime(TimeOnly.MinValue);
        }

        /// <summary>
        /// Moves a date to the start of its containing day, Monday-based week, month, or year.
        /// </summary>
        /// <param name="dateUnit">The calendar boundary to use.</param>
        /// <returns>The start of the containing calendar unit.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="dateUnit"/> is unsupported.</exception>
        [DebuggerStepThrough]
        public DateOnly FloorTo(DateUnit dateUnit)
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

        /// <summary>
        /// Moves a date to the next calendar-unit boundary unless it already lies on that boundary.
        /// </summary>
        /// <param name="dateUnit">The day, Monday-based week, month, or year boundary to use.</param>
        /// <returns>The source when already aligned; otherwise, the next boundary.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="dateUnit"/> is unsupported.</exception>
        [DebuggerStepThrough]
        public DateOnly CeilingTo(DateUnit dateUnit)
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

        /// <summary>
        /// Aligns a date to the start of its containing calendar unit.
        /// </summary>
        /// <param name="dateUnit">The calendar unit whose lower boundary should be returned.</param>
        /// <returns>The same value as <see cref="FloorTo(DateOnly, DateUnit)"/>.</returns>
        [DebuggerStepThrough]
        public DateOnly RoundToNearest(DateUnit dateUnit)
        {
            return source.FloorTo(dateUnit);
        }

        /// <summary>
        /// Formats a date using ISO yyyy-MM-dd format.
        /// </summary>
        /// <returns>The invariant ISO date string.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var text = new DateOnly(2026, 6, 29).ToIsoDateString();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public string ToIsoDateString()
        {
            return source.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
