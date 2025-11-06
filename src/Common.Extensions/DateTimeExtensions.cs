// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

public static class DateTimeExtensions
{
    /// <summary>
    /// Gets the start of the day for the specified DateTime.
    /// </summary>
    /// <param name="source">The DateTime to get the start of the day for.</param>
    /// <returns>A DateTime representing the start of the day.</returns>
    [DebuggerStepThrough]
    public static DateTime StartOfDay(this DateTime source)
    {
        return new DateTime(source.Year, source.Month, source.Day, 0, 0, 0, 0, source.Kind);
    }

    /// <summary>
    /// Gets the end of the day for the specified DateTime.
    /// </summary>
    /// <param name="source">The DateTime to get the end of the day for.</param>
    /// <returns>A DateTime representing the end of the day.</returns>
    [DebuggerStepThrough]
    public static DateTime EndOfDay(this DateTime source)
    {
        return source.StartOfDay().AddDays(1).AddSeconds(-1);
    }

    /// <summary>
    /// Gets the start of the week for the specified DateTime with a defined first day of the week.
    /// </summary>
    /// <param name="source">The DateTime to get the start of the week for.</param>
    /// <param name="day">The first day of the week.</param>
    /// <returns>A DateTime representing the start of the week.</returns>
    [DebuggerStepThrough]
    public static DateTime StartOfWeek(this DateTime source, DayOfWeek day = DayOfWeek.Monday)
    {
        var offset = source.DayOfWeek - day;
        if (offset < 0)
        {
            offset += 7;
        }

        return source.AddDays(-1 * offset).StartOfDay();
    }

    /// <summary>
    /// Gets the end of the week for the specified DateTime.
    /// </summary>
    /// <param name="source">The DateTime to get the end of the week for.</param>
    /// <returns>A DateTime representing the end of the week.</returns>
    [DebuggerStepThrough]
    public static DateTime EndOfWeek(this DateTime source)
    {
        return source.StartOfWeek().AddDays(7).AddSeconds(-1);
    }

    /// <summary>
    /// Gets the start of the month for the specified DateTime.
    /// </summary>
    /// <param name="source">The DateTime to get the start of the month for.</param>
    /// <returns>A DateTime representing the start of the month.</returns>
    [DebuggerStepThrough]
    public static DateTime StartOfMonth(this DateTime source)
    {
        return new DateTime(source.Year, source.Month, 1, 0, 0, 0, 0, source.Kind);
    }

    /// <summary>
    /// Gets the end of the month for the specified DateTime.
    /// </summary>
    /// <param name="source">The DateTime to get the end of the month for.</param>
    /// <returns>A DateTime representing the end of the month.</returns>
    [DebuggerStepThrough]
    public static DateTime EndOfMonth(this DateTime source)
    {
        return source.StartOfMonth().AddMonths(1).AddSeconds(-1);
    }

    /// <summary>
    /// Gets the start of the year for the specified DateTime.
    /// </summary>
    /// <param name="source">The DateTime to get the start of the year for.</param>
    /// <returns>A DateTime representing the start of the year.</returns>
    [DebuggerStepThrough]
    public static DateTime StartOfYear(this DateTime source)
    {
        return new DateTime(source.Year, 1, 1, 0, 0, 0, 0, source.Kind);
    }

    /// <summary>
    /// Gets the end of the year for the specified DateTime.
    /// </summary>
    /// <param name="source">The DateTime to get the end of the year for.</param>
    /// <returns>A DateTime representing the end of the year.</returns>
    [DebuggerStepThrough]
    public static DateTime EndOfYear(this DateTime source)
    {
        return source.StartOfYear().AddYears(1).AddSeconds(-1);
    }

    [DebuggerStepThrough]
    public static DateTime? ParseDateOrEpoch(this string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return null;
        }

        // Try parsing as epoch (Unix timestamp)
        if (long.TryParse(source, out var epoch) && epoch is > 99999999 or < 0) // don't clash with compact format
        {
            return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
        }

        var formats = new[]
        {
            "yyyy-MM-dd", // ISO 8601 (e.g., "2024-03-14")
            "yyyy-MM-ddTHH:mm:ss", // ISO 8601 with time (e.g., "2024-03-14T13:45:30")
            "yyyy-MM-ddTHH:mm:ssZ", // ISO 8601 with UTC (e.g., "2024-03-14T13:45:30Z")
            "yyyy-MM-ddTHH:mm:ss.fffffff", // ISO 8601 with milliseconds (e.g., "2024-03-14T13:45:30.1234567")
            "dd/MM/yyyy", // UK format (e.g., "14/03/2024")
            "MM/dd/yyyy", // US format (e.g., "03/14/2024")
            "dd-MM-yyyy", // Alternative format (e.g., "14-03-2024")
            "dd.MM.yyyy", // European format (e.g., "14.03.2024")
            "yyyyMMdd", // Compact format (e.g., "20240314")
            "dd MMM yyyy", // Month name format (e.g., "14 Mar 2024")
            "d MMMM yyyy" // Full month name format (e.g., "14 March 2024")
        };

        // Try parsing as ISO 8601 date string
        if (DateTime.TryParseExact(
                source,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        throw new ArgumentException($"Invalid date format: {source}. Useany format or Unix epoch.");
    }

    [DebuggerStepThrough]
    public static bool TryParseDateOrEpoch(this string source, out DateTime result)
    {
        result = DateTime.MinValue;

        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        // Try parsing as epoch (Unix timestamp)
        if (long.TryParse(source, out var epoch) && epoch is > 99999999 or < 0) // don't clash with compact format
        {
            try
            {
                result = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Define accepted formats
        var formats = new[]
        {
            "yyyy-MM-dd", // ISO 8601 (e.g., "2024-03-14")
            "yyyy-MM-ddTHH:mm:ss", // ISO 8601 with time (e.g., "2024-03-14T13:45:30")
            "yyyy-MM-ddTHH:mm:ssZ", // ISO 8601 with UTC (e.g., "2024-03-14T13:45:30Z")
            "yyyy-MM-ddTHH:mm:ss.fffffff", // ISO 8601 with milliseconds (e.g., "2024-03-14T13:45:30.1234567")
            "dd/MM/yyyy", // UK format (e.g., "14/03/2024")
            "MM/dd/yyyy", // US format (e.g., "03/14/2024")
            "dd-MM-yyyy", // Alternative format (e.g., "14-03-2024")
            "dd.MM.yyyy", // European format (e.g., "14.03.2024")
            "yyyyMMdd", // Compact format (e.g., "20240314")
            "dd MMM yyyy", // Month name format (e.g., "14 Mar 2024")
            "d MMMM yyyy" // Full month name format (e.g., "14 March 2024")
        };

        // Try parsing with exact formats first
        if (DateTime.TryParseExact(
                source,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exactDate))
        {
            result = exactDate;

            return true;
        }

        // Try general parsing as fallback
        return DateTime.TryParse(
            source,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }

    [DebuggerStepThrough]
    public static DateTime Add(this DateTime date, DateUnit unit, int amount)
    {
        return unit switch
        {
            DateUnit.Day => date.AddDays(amount),
            DateUnit.Week => date.AddDays(7 * amount),
            DateUnit.Month => AddMonths(date, amount),
            DateUnit.Year => AddMonths(date, amount * 12),
            _ => throw new ArgumentException("Unsupported DateUnit.", nameof(unit))
        };
    }

    [DebuggerStepThrough]
    private static DateTime AddMonths(DateTime date, int months)
    {
        // Calculate the target year and month
        var totalMonths = date.Year * 12 + (date.Month - 1) + months;
        var targetYear = totalMonths / 12;
        var targetMonth = totalMonths % 12 + 1;

        // Determine the last day of the target month to avoid invalid dates
        var daysInTargetMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        var targetDay = Math.Min(date.Day, daysInTargetMonth);

        return new DateTime(targetYear, targetMonth, targetDay, date.Hour, date.Minute, date.Second, date.Millisecond, date.Kind);
    }

    [DebuggerStepThrough]
    public static bool IsInRange(this DateTime date, DateTime start, DateTime end, bool inclusive = true)
    {
        return inclusive ? date >= start && date <= end : date > start && date < end;
    }

    /// <summary>
    /// Determines whether a given date is within a specified relative range from the current date.
    /// </summary>
    /// <param name="date">The date to be evaluated.</param>
    /// <param name="unit">The unit of time for the range (e.g., Days, Weeks, Months, Years).</param>
    /// <param name="amount">The amount of the unit to define the range.</param>
    /// <param name="direction">The direction relative to the current date (Past or Future).</param>
    /// <param name="inclusive">Specifies whether the range should be inclusive of the boundary dates.</param>
    /// <returns>True if the date is within the specified relative range; otherwise, false.</returns>
    [DebuggerStepThrough]
    public static bool IsInRelativeRange(this DateTime date, DateUnit unit, int amount, DateTimeDirection direction, bool inclusive = true)
    {
        var now = DateTime.Now;
        var referenceDate = direction == DateTimeDirection.Past ? now.Add(unit, -amount) : now.Add(unit, amount);

        return direction == DateTimeDirection.Past
            ? (inclusive ? date <= now && date >= referenceDate : date < now && date > referenceDate)
            : (inclusive ? date >= now && date <= referenceDate : date > now && date < referenceDate);
    }

    [DebuggerStepThrough]
    public static int GetWeekOfYear(this DateTime date)
    {
        var cultureInfo = CultureInfo.InvariantCulture;
        var calendar = cultureInfo.Calendar;

        return calendar.GetWeekOfYear(date, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
    }

    [DebuggerStepThrough]
    public static bool IsLeapYear(this DateTime date)
    {
        return DateTime.IsLeapYear(date.Year);
    }

    [DebuggerStepThrough]
    public static int DaysUntil(this DateTime date)
    {
        return (date - DateTime.Now).Days;
    }

    [DebuggerStepThrough]
    public static long ToUnixTimeSeconds(this DateTime date)
    {
        return new DateTimeOffset(date).ToUnixTimeSeconds();
    }

    [DebuggerStepThrough]
    public static DateTimeOffset ToDateTimeOffset(this DateTime date, TimeSpan? offset = null)
    {
        return new DateTimeOffset(date, offset ?? TimeSpan.Zero);
    }

    [DebuggerStepThrough]
    public static TimeSpan TimeSpanTo(this DateTime date, DateTime target)
    {
        return target - date;
    }

    /// <summary>
    /// Converts a <see cref="DateTime"/> value to a <see cref="DateOnly"/> preserving the calendar date and discarding the time component.
    /// </summary>
    /// <param name="source">The source <see cref="DateTime"/> value.</param>
    /// <returns>A <see cref="DateOnly"/> representing the date portion of the original <see cref="DateTime"/>.</returns>
    [DebuggerStepThrough]
    public static DateOnly ToDateOnly(this DateTime source)
    {
        return DateOnly.FromDateTime(source);
    }

    /// <summary>
    /// Converts a <see cref="DateTime"/> value to a <see cref="TimeOnly"/> preserving only the time-of-day component and discarding the date component.
    /// </summary>
    /// <param name="source">The source <see cref="DateTime"/> value.</param>
    /// <returns>A <see cref="TimeOnly"/> representing the time portion of the original <see cref="DateTime"/>.</returns>
    [DebuggerStepThrough]
    public static TimeOnly ToTimeOnly(this DateTime source)
    {
        return TimeOnly.FromDateTime(source);
    }

    [DebuggerStepThrough]
    public static DateTime RoundToNearest(this DateTime dateTime, DateUnit dateUnit)
    {
        switch (dateUnit)
        {
            case DateUnit.Day:
                return dateTime.StartOfDay();
            case DateUnit.Week:
                return dateTime.StartOfWeek();
            case DateUnit.Month:
                return dateTime.StartOfMonth();
            case DateUnit.Year:
                return dateTime.StartOfYear();
            default:
                throw new ArgumentException("Unsupported DateUnit.", nameof(dateUnit));
        }
    }

    [DebuggerStepThrough]
    public static DateTime RoundToNearest(this DateTime dateTime, TimeUnit timeUnit)
    {
        switch (timeUnit)
        {
            case TimeUnit.Minute:
                return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, (dateTime.Minute / 1) * 1, 0, dateTime.Kind);
            case TimeUnit.Hour:
                return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, (dateTime.Hour / 1) * 1, 0, 0, dateTime.Kind);
            default:
                throw new ArgumentException("Unsupported TimeUnit.", nameof(timeUnit));
        }
    }

    [DebuggerStepThrough]
    public static DateTime AddBusinessDays(this DateTime dateTime, int days, DateTime[] holidays, params DayOfWeek[] nonWorkingDays)
    {
        if (days == 0)
        {
            return dateTime;
        }

        // Use default non-working days if none specified
        if (nonWorkingDays == null || nonWorkingDays.Length == 0)
        {
            nonWorkingDays = [DayOfWeek.Saturday, DayOfWeek.Sunday];
        }

        var result = dateTime;
        var step = days > 0 ? 1 : -1;
        var absDays = Math.Abs(days);
        var addedDays = 0;

        while (addedDays < absDays)
        {
            result = result.AddDays(step);
            if (IsBusinessDay(result, holidays, nonWorkingDays))
            {
                addedDays++;
            }
        }

        return result;
    }

    private static bool IsBusinessDay(DateTime dateTime, DateTime[] holidays, DayOfWeek[] nonWorkingDays)
    {
        // Check if the date is a non-working day
        if (Array.Exists(nonWorkingDays, day => day == dateTime.DayOfWeek))
        {
            return false;
        }

        // Check if the date is in the list of holidays
        return holidays == null || !holidays.Any(holiday => holiday.Date == dateTime.Date);
    }
}

/// <summary>
/// Specifies the unit of time for date-based relative operations.
/// </summary>
public enum DateUnit
{
    /// <summary>
    /// Represents a day unit for relative date calculations.
    /// </summary>
    Day,

    /// <summary>
    /// Represents a week unit (7 days) for relative date calculations.
    /// </summary>
    Week,

    /// <summary>
    /// Represents a month unit (30 days) for relative date calculations.
    /// </summary>
    Month,

    /// <summary>
    /// Represents a year unit (365 days) for relative date calculations.
    /// </summary>
    Year
}

/// <summary>
/// Specifies the unit of time for time-based relative operations.
/// </summary>
public enum TimeUnit
{
    /// <summary>
    /// Represents a minute unit for relative time calculations.
    /// </summary>
    Minute,

    /// <summary>
    /// Represents an hour unit for relative time calculations.
    /// </summary>
    Hour
}

/// <summary>
/// Specifies the direction for relative date/time calculations.
/// </summary>
public enum DateTimeDirection
{
    /// <summary>
    /// Indicates that the calculation should look backward in time from the reference point.
    /// </summary>
    Past,

    /// <summary>
    /// Indicates that the calculation should look forward in time from the reference point.
    /// </summary>
    Future
}