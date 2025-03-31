// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System;

/// <summary>
/// A fluent builder for constructing Quartz.NET 6-field cron expressions (seconds, minutes, hours, day of month, month, day of week).
/// </summary>
/// <remarks>
/// See https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html for cron syntax details.
/// The 6-field format is: [Seconds] [Minutes] [Hours] [Day of Month] [Month] [Day of Week].
/// </remarks>
public class CronExpressionBuilder
{
    private string seconds = "0"; // Default: start of the minute
    private string minutes = "*"; // Default: every minute
    private string hours = "*";   // Default: every hour
    private string dayOfMonth = "*"; // Default: every day
    private string month = "*";   // Default: every month
    private string dayOfWeek = "?"; // Default: no specific day of week

    /// <summary>
    /// Sets the seconds field to a specific value.
    /// </summary>
    /// <param name="value">The seconds value (0-59).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run at 5 seconds past the minute
    /// var cron = new CronExpressionBuilder()
    ///     .Seconds(5)
    ///     .Build(); // "5 * * * * ?"
    /// </example>
    public CronExpressionBuilder Seconds(int value)
    {
        if (value < 0 || value > 59) throw new ArgumentOutOfRangeException(nameof(value), "Seconds must be between 0 and 59.");
        this.seconds = value.ToString();
        return this;
    }

    /// <summary>
    /// Sets the seconds field to a range of values.
    /// </summary>
    /// <param name="start">The starting second (0-59).</param>
    /// <param name="end">The ending second (0-59, must be >= start).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run from 0 to 10 seconds past the minute
    /// var cron = new CronExpressionBuilder()
    ///     .SecondsRange(0, 10)
    ///     .Build(); // "0-10 * * * * ?"
    /// </example>
    public CronExpressionBuilder SecondsRange(int start, int end)
    {
        if (start < 0 || start > 59) throw new ArgumentOutOfRangeException(nameof(start), "Start seconds must be between 0 and 59.");
        if (end < start || end > 59) throw new ArgumentOutOfRangeException(nameof(end), "End seconds must be between start and 59.");
        this.seconds = $"{start}-{end}";
        return this;
    }

    /// <summary>
    /// Sets the minutes field to a specific value.
    /// </summary>
    /// <param name="value">The minutes value (0-59).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run at 15 minutes past the hour
    /// var cron = new CronExpressionBuilder()
    ///     .Minutes(15)
    ///     .Build(); // "0 15 * * * ?"
    /// </example>
    public CronExpressionBuilder Minutes(int value)
    {
        if (value < 0 || value > 59) throw new ArgumentOutOfRangeException(nameof(value), "Minutes must be between 0 and 59.");
        this.minutes = value.ToString();
        return this;
    }

    /// <summary>
    /// Sets the minutes field to a range of values.
    /// </summary>
    /// <param name="start">The starting minute (0-59).</param>
    /// <param name="end">The ending minute (0-59, must be >= start).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run from 0 to 30 minutes past the hour
    /// var cron = new CronExpressionBuilder()
    ///     .MinutesRange(0, 30)
    ///     .Build(); // "0 0-30 * * * ?"
    /// </example>
    public CronExpressionBuilder MinutesRange(int start, int end)
    {
        if (start < 0 || start > 59) throw new ArgumentOutOfRangeException(nameof(start), "Start minutes must be between 0 and 59.");
        if (end < start || end > 59) throw new ArgumentOutOfRangeException(nameof(end), "End minutes must be between start and 59.");
        this.minutes = $"{start}-{end}";
        return this;
    }

    /// <summary>
    /// Sets the hours field to a specific value.
    /// </summary>
    /// <param name="value">The hours value (0-23).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run at 9 AM
    /// var cron = new CronExpressionBuilder()
    ///     .Hours(9)
    ///     .Build(); // "0 * 9 * * ?"
    /// </example>
    public CronExpressionBuilder Hours(int value)
    {
        if (value < 0 || value > 23) throw new ArgumentOutOfRangeException(nameof(value), "Hours must be between 0 and 23.");
        this.hours = value.ToString();
        return this;
    }

    /// <summary>
    /// Sets the hours field to a range of values.
    /// </summary>
    /// <param name="start">The starting hour (0-23).</param>
    /// <param name="end">The ending hour (0-23, must be >= start).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run from 8 AM to 5 PM
    /// var cron = new CronExpressionBuilder()
    ///     .HoursRange(8, 17)
    ///     .Build(); // "0 * 8-17 * * ?"
    /// </example>
    public CronExpressionBuilder HoursRange(int start, int end)
    {
        if (start < 0 || start > 23) throw new ArgumentOutOfRangeException(nameof(start), "Start hours must be between 0 and 23.");
        if (end < start || end > 23) throw new ArgumentOutOfRangeException(nameof(end), "End hours must be between start and 23.");
        this.hours = $"{start}-{end}";
        return this;
    }

    /// <summary>
    /// Sets the day of month field to a specific value.
    /// </summary>
    /// <param name="value">The day of month value (1-31).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run on the 15th of every month at 12:59 PM
    /// var cron = new CronExpressionBuilder()
    ///     .DayOfMonth(15)
    ///     .Hours(12)
    ///     .Minutes(59)
    ///     .Build(); // "0 59 12 15 * ?"
    /// </example>
    public CronExpressionBuilder DayOfMonth(int value)
    {
        if (value < 1 || value > 31) throw new ArgumentOutOfRangeException(nameof(value), "Day of month must be between 1 and 31.");
        this.dayOfMonth = value.ToString();
        this.dayOfWeek = "?"; // Mutually exclusive with day of week
        return this;
    }

    /// <summary>
    /// Sets the day of month field to a range of values.
    /// </summary>
    /// <param name="start">The starting day of month (1-31).</param>
    /// <param name="end">The ending day of month (1-31, must be >= start).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run from the 1st to the 10th of every month at midnight
    /// var cron = new CronExpressionBuilder()
    ///     .DayOfMonthRange(1, 10)
    ///     .AtTime(0, 0)
    ///     .Build(); // "0 0 0 1-10 * ?"
    /// </example>
    public CronExpressionBuilder DayOfMonthRange(int start, int end)
    {
        if (start < 1 || start > 31) throw new ArgumentOutOfRangeException(nameof(start), "Start day of month must be between 1 and 31.");
        if (end < start || end > 31) throw new ArgumentOutOfRangeException(nameof(end), "End day of month must be between start and 31.");
        this.dayOfMonth = $"{start}-{end}";
        this.dayOfWeek = "?"; // Mutually exclusive with day of week
        return this;
    }

    /// <summary>
    /// Sets the day of month field to a specific value or special identifier using a string.
    /// </summary>
    /// <param name="value">The day of month value (e.g., "L" for last day). Defaults to "*" if null or empty.</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run on the last day of every month at midnight
    /// var cron = new CronExpressionBuilder()
    ///     .DayOfMonth("L")
    ///     .AtTime(0, 0)
    ///     .Build(); // "0 0 0 L * ?"
    /// </example>
    public CronExpressionBuilder DayOfMonth(string value)
    {
        this.dayOfMonth = string.IsNullOrEmpty(value) ? "*" : value;
        this.dayOfWeek = "?"; // Mutually exclusive with day of week
        return this;
    }

    /// <summary>
    /// Sets the month field to a specific value using an enum.
    /// </summary>
    /// <param name="month">The month value from CronMonth enum (e.g., CronMonth.January).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run every day in January
    /// var cron = new CronExpressionBuilder()
    ///     .Month(CronMonth.January)
    ///     .Build(); // "0 * * * JAN ?"
    /// </example>
    public CronExpressionBuilder Month(CronMonth month)
    {
        this.month = month switch
        {
            CronMonth.January => "JAN",
            CronMonth.February => "FEB",
            CronMonth.March => "MAR",
            CronMonth.April => "APR",
            CronMonth.May => "MAY",
            CronMonth.June => "JUN",
            CronMonth.July => "JUL",
            CronMonth.August => "AUG",
            CronMonth.September => "SEP",
            CronMonth.October => "OCT",
            CronMonth.November => "NOV",
            CronMonth.December => "DEC",
            _ => throw new ArgumentOutOfRangeException(nameof(month), "Invalid CronMonth value")
        };
        return this;
    }

    /// <summary>
    /// Sets the month field to a specific value or range using a string.
    /// </summary>
    /// <param name="value">The month value (e.g., "1", "JAN-MAR"). Defaults to "*" if null or empty.</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run every day in January to March
    /// var cron = new CronExpressionBuilder()
    ///     .Month("JAN-MAR")
    ///     .Build(); // "0 * * * JAN-MAR ?"
    /// </example>
    public CronExpressionBuilder Month(string value)
    {
        this.month = string.IsNullOrEmpty(value) ? "*" : value;
        return this;
    }

    /// <summary>
    /// Sets the day of week field to a specific value using an enum.
    /// </summary>
    /// <param name="day">The day of week value from CronDayOfWeek enum (e.g., CronDayOfWeek.Monday).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run every Wednesday at 12:59 PM
    /// var cron = new CronExpressionBuilder()
    ///     .DayOfMonth(15)
    ///     .DayOfWeek(CronDayOfWeek.Wednesday)
    ///     .Hours(12)
    ///     .Minutes(59)
    ///     .Build(); // "0 59 12 ? * WED"
    /// </example>
    public CronExpressionBuilder DayOfWeek(CronDayOfWeek day)
    {
        this.dayOfWeek = day switch
        {
            CronDayOfWeek.Sunday => "SUN",
            CronDayOfWeek.Monday => "MON",
            CronDayOfWeek.Tuesday => "TUE",
            CronDayOfWeek.Wednesday => "WED",
            CronDayOfWeek.Thursday => "THU",
            CronDayOfWeek.Friday => "FRI",
            CronDayOfWeek.Saturday => "SAT",
            _ => throw new ArgumentOutOfRangeException(nameof(day), "Invalid CronDayOfWeek value")
        };
        this.dayOfMonth = "?"; // Mutually exclusive with day of month
        return this;
    }

    /// <summary>
    /// Sets the day of week field to a specific value or range using a string.
    /// </summary>
    /// <param name="value">The day of week value (e.g., "MON", "MON-FRI"). Defaults to "?" if null or empty.</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run every Wednesday at 12:59 PM
    /// var cron = new CronExpressionBuilder()
    ///     .DayOfMonth(15)
    ///     .DayOfWeek("WED")
    ///     .Hours(12)
    ///     .Minutes(59)
    ///     .Build(); // "0 59 12 ? * WED"
    /// </example>
    public CronExpressionBuilder DayOfWeek(string value)
    {
        this.dayOfWeek = string.IsNullOrEmpty(value) ? "?" : value;
        this.dayOfMonth = "?"; // Mutually exclusive with day of month
        return this;
    }

    /// <summary>
    /// Sets the time of day for the cron expression (hours, minutes, and optionally seconds).
    /// </summary>
    /// <param name="hour">The hour (0-23).</param>
    /// <param name="minute">The minute (0-59).</param>
    /// <param name="second">The second (0-59, optional, defaults to 0).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run daily at 9:30:15 AM
    /// var cron = new CronExpressionBuilder()
    ///     .AtTime(9, 30, 15)
    ///     .Build(); // "15 30 9 * * ?"
    /// </example>
    public CronExpressionBuilder AtTime(int hour, int minute, int second = 0)
    {
        if (hour < 0 || hour > 23) throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23.");
        if (minute < 0 || minute > 59) throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59.");
        if (second < 0 || second > 59) throw new ArgumentOutOfRangeException(nameof(second), "Second must be between 0 and 59.");

        this.hours = hour.ToString();
        this.minutes = minute.ToString();
        this.seconds = second.ToString();
        return this;
    }

    /// <summary>
    /// Sets the time of day for the cron expression using a DateTimeOffset.
    /// </summary>
    /// <param name="time">The DateTimeOffset specifying the time of day (hour, minute, second).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run daily at 14:45:30 (2:45:30 PM)
    /// var time = new DateTimeOffset(2023, 1, 1, 14, 45, 30, TimeSpan.Zero);
    /// var cron = new CronExpressionBuilder()
    ///     .AtTime(time)
    ///     .Build(); // "30 45 14 * * ?"
    /// </example>
    public CronExpressionBuilder AtTime(DateTimeOffset time)
    {
        this.hours = time.Hour.ToString();
        this.minutes = time.Minute.ToString();
        this.seconds = time.Second.ToString();
        return this;
    }

    /// <summary>
    /// Sets the cron expression to trigger exactly once at a specific date and time.
    /// </summary>
    /// <param name="dateTime">The DateTimeOffset specifying the exact date and time for the single execution.</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run once on March 27, 2025, at 14:30:00 (2:30 PM)
    /// var dateTime = new DateTimeOffset(2025, 3, 27, 14, 30, 0, TimeSpan.Zero);
    /// var cron = new CronExpressionBuilder()
    ///     .AtDateTime(dateTime)
    ///     .Build(); // "0 30 14 27 3 ?"
    /// </example>
    /// <remarks>
    /// This creates a cron expression that matches only the specified date and time within the year.
    /// For true one-time execution, consider using a Quartz SimpleTrigger instead of a cron trigger.
    /// </remarks>
    public CronExpressionBuilder AtDateTime(DateTimeOffset dateTime)
    {
        this.seconds = dateTime.Second.ToString();
        this.minutes = dateTime.Minute.ToString();
        this.hours = dateTime.Hour.ToString();
        this.dayOfMonth = dateTime.Day.ToString();
        this.month = dateTime.Month.ToString(); // Numeric month (1-12)
        this.dayOfWeek = "?"; // Day of week is irrelevant with specific day of month
        return this;
    }

    /// <summary>
    /// Sets the expression to run every specified number of seconds.
    /// </summary>
    /// <param name="interval">The interval in seconds (1-59).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run every 15 seconds
    /// var cron = new CronExpressionBuilder()
    ///     .EverySeconds(15)
    ///     .Build(); // "0/15 * * * * ?"
    /// </example>
    public CronExpressionBuilder EverySeconds(int interval)
    {
        if (interval < 1 || interval > 59) throw new ArgumentOutOfRangeException(nameof(interval), "Seconds interval must be between 1 and 59.");

        this.seconds = $"0/{interval}";
        return this;
    }

    /// <summary>
    /// Sets the expression to run every specified number of minutes.
    /// </summary>
    /// <param name="interval">The interval in minutes (1-59).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run every 5 minutes
    /// var cron = new CronExpressionBuilder()
    ///     .EveryMinutes(5)
    ///     .Build(); // "0 0/5 * * * ?"
    /// </example>
    public CronExpressionBuilder EveryMinutes(int interval)
    {
        if (interval < 1 || interval > 59) throw new ArgumentOutOfRangeException(nameof(interval), "Minutes interval must be between 1 and 59.");

        this.minutes = $"0/{interval}";
        this.seconds = "0"; // Reset to start of minute
        return this;
    }

    /// <summary>
    /// Sets the expression to run every specified number of hours.
    /// </summary>
    /// <param name="interval">The interval in hours (1-23).</param>
    /// <returns>The current CronExpressionBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Run every 6 hours
    /// var cron = new CronExpressionBuilder()
    ///     .EveryHours(6)
    ///     .Build(); // "0 0 */6 * * ?"
    /// </example>
    public CronExpressionBuilder EveryHours(int interval)
    {
        if (interval < 1 || interval > 23) throw new ArgumentOutOfRangeException(nameof(interval), "Hours interval must be between 1 and 23.");

        this.hours = $"*/{interval}";
        this.minutes = "0"; // Reset to start of hour
        this.seconds = "0"; // Reset to start of minute
        return this;
    }

    /// <summary>
    /// Builds the final 6-field cron expression string.
    /// </summary>
    /// <returns>The constructed cron expression as a string.</returns>
    /// <example>
    /// // Build a cron for a specific date and time: 15th of the month, Wednesday, at 12:59 PM
    /// var cron = new CronExpressionBuilder()
    ///     .DayOfMonth(15)
    ///     .DayOfWeek(CronDayOfWeek.Wednesday)
    ///     .Hours(12)
    ///     .Minutes(59)
    ///     .Build(); // "0 59 12 ? * WED"
    /// </example>
    public string Build()
    {
        return $"{this.seconds} {this.minutes} {this.hours} {this.dayOfMonth} {this.month} {this.dayOfWeek}";
    }
}

/// <summary>
/// Enum representing days of the week for use in Quartz.NET cron expressions.
/// </summary>
public enum CronDayOfWeek
{
    Sunday,
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday
}

/// <summary>
/// Enum representing months for use in Quartz.NET cron expressions.
/// </summary>
public enum CronMonth
{
    January = 1,
    February,
    March,
    April,
    May,
    June,
    July,
    August,
    September,
    October,
    November,
    December
}