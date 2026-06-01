// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Fluent builder for Jobs cron expressions.
/// </summary>
/// <remarks>
/// Builds five-field standard cron expressions by default. Seconds-based methods switch to the six-field Jobs format supported by <see cref="CronosJobCronEngine"/>.
/// </remarks>
/// <example>
/// <code>
/// var cron = new CronExpressionBuilder().DailyAt(2, 30).Build();
/// </code>
/// </example>
public class CronExpressionBuilder
{
    private string seconds;
    private string minutes = "*";
    private string hours = "*";
    private string dayOfMonth = "*";
    private string month = "*";
    private string dayOfWeek = "*";

    /// <summary>Sets the seconds field and enables six-field output.</summary>
    /// <param name="value">The seconds value from 0 to 59.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().Seconds(5).Build();</code></example>
    public CronExpressionBuilder Seconds(int value)
    {
        if (value is < 0 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Seconds must be between 0 and 59.");
        }

        this.seconds = value.ToString();

        return this;
    }

    /// <summary>Sets the seconds field to a range and enables six-field output.</summary>
    /// <param name="start">The starting second from 0 to 59.</param>
    /// <param name="end">The ending second from 0 to 59.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().SecondsRange(0, 10).Build();</code></example>
    public CronExpressionBuilder SecondsRange(int start, int end)
    {
        ValidateRange(start, end, 0, 59, nameof(start), nameof(end), "seconds");

        this.seconds = $"{start}-{end}";

        return this;
    }

    /// <summary>Sets the minutes field.</summary>
    /// <param name="value">The minutes value from 0 to 59.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().Minutes(15).Build();</code></example>
    public CronExpressionBuilder Minutes(int value)
    {
        if (value is < 0 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Minutes must be between 0 and 59.");
        }

        this.minutes = value.ToString();

        return this;
    }

    /// <summary>Sets the minutes field to a range.</summary>
    /// <param name="start">The starting minute from 0 to 59.</param>
    /// <param name="end">The ending minute from 0 to 59.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().MinutesRange(0, 30).Build();</code></example>
    public CronExpressionBuilder MinutesRange(int start, int end)
    {
        ValidateRange(start, end, 0, 59, nameof(start), nameof(end), "minutes");

        this.minutes = $"{start}-{end}";

        return this;
    }

    /// <summary>Sets the hours field.</summary>
    /// <param name="value">The hours value from 0 to 23.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().Hours(9).Build();</code></example>
    public CronExpressionBuilder Hours(int value)
    {
        if (value is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Hours must be between 0 and 23.");
        }

        this.hours = value.ToString();

        return this;
    }

    /// <summary>Sets the hours field to a range.</summary>
    /// <param name="start">The starting hour from 0 to 23.</param>
    /// <param name="end">The ending hour from 0 to 23.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().HoursRange(8, 17).Build();</code></example>
    public CronExpressionBuilder HoursRange(int start, int end)
    {
        ValidateRange(start, end, 0, 23, nameof(start), nameof(end), "hours");

        this.hours = $"{start}-{end}";

        return this;
    }

    /// <summary>Sets the day-of-month field.</summary>
    /// <param name="value">The day of month value from 1 to 31.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().DayOfMonth(1).Build();</code></example>
    public CronExpressionBuilder DayOfMonth(int value)
    {
        if (value is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Day of month must be between 1 and 31.");
        }

        this.dayOfMonth = value.ToString();

        return this;
    }

    /// <summary>Sets the day-of-month field to a range.</summary>
    /// <param name="start">The starting day of month from 1 to 31.</param>
    /// <param name="end">The ending day of month from 1 to 31.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().DayOfMonthRange(1, 10).Build();</code></example>
    public CronExpressionBuilder DayOfMonthRange(int start, int end)
    {
        ValidateRange(start, end, 1, 31, nameof(start), nameof(end), "days of month");

        this.dayOfMonth = $"{start}-{end}";

        return this;
    }

    /// <summary>Sets the day-of-month field to a raw cron value.</summary>
    /// <param name="value">The day-of-month value. Empty values reset to wildcard.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().DayOfMonth("L").Build();</code></example>
    public CronExpressionBuilder DayOfMonth(string value)
    {
        this.dayOfMonth = string.IsNullOrWhiteSpace(value) ? "*" : value;

        return this;
    }

    /// <summary>Sets the month field.</summary>
    /// <param name="value">The month value.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().Month(CronMonth.January).Build();</code></example>
    public CronExpressionBuilder Month(CronMonth value)
    {
        this.month = value.ToString()[..3].ToUpperInvariant();

        return this;
    }

    /// <summary>Sets the month field to a raw cron value.</summary>
    /// <param name="value">The month value. Empty values reset to wildcard.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().Month("JAN-MAR").Build();</code></example>
    public CronExpressionBuilder Month(string value)
    {
        this.month = string.IsNullOrWhiteSpace(value) ? "*" : value;

        return this;
    }

    /// <summary>Sets the day-of-week field.</summary>
    /// <param name="value">The day of week value.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().DayOfWeek(CronDayOfWeek.Monday).Build();</code></example>
    public CronExpressionBuilder DayOfWeek(CronDayOfWeek value)
    {
        this.dayOfWeek = value.ToString()[..3].ToUpperInvariant();

        return this;
    }

    /// <summary>Sets the day-of-week field to a raw cron value.</summary>
    /// <param name="value">The day-of-week value. Empty values reset to wildcard.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().DayOfWeek("MON-FRI").Build();</code></example>
    public CronExpressionBuilder DayOfWeek(string value)
    {
        this.dayOfWeek = string.IsNullOrWhiteSpace(value) ? "*" : value;

        return this;
    }

    /// <summary>Sets the expression to run daily at the specified time.</summary>
    /// <param name="hour">The hour from 0 to 23.</param>
    /// <param name="minute">The minute from 0 to 59.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().DailyAt(2, 0).Build();</code></example>
    public CronExpressionBuilder DailyAt(int hour, int minute)
    {
        return this.Hours(hour).Minutes(minute).DayOfMonth("*").Month("*").DayOfWeek("*");
    }

    /// <summary>Sets the time of day.</summary>
    /// <param name="hour">The hour from 0 to 23.</param>
    /// <param name="minute">The minute from 0 to 59.</param>
    /// <param name="second">The second from 0 to 59. Non-zero values enable six-field output.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().AtTime(9, 30).Build();</code></example>
    public CronExpressionBuilder AtTime(int hour, int minute, int second = 0)
    {
        this.Hours(hour);
        this.Minutes(minute);

        if (second is < 0 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(second), "Second must be between 0 and 59.");
        }

        this.seconds = second == 0 ? null : second.ToString();

        return this;
    }

    /// <summary>Sets the time of day.</summary>
    /// <param name="time">The time value.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().AtTime(DateTimeOffset.UtcNow).Build();</code></example>
    public CronExpressionBuilder AtTime(DateTimeOffset time)
    {
        return this.AtTime(time.Hour, time.Minute, time.Second);
    }

    /// <summary>Sets the expression to match the supplied date and time.</summary>
    /// <param name="dateTime">The date and time.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().AtDateTime(new DateTimeOffset(2026, 6, 2, 9, 30, 0, TimeSpan.Zero)).Build();</code></example>
    public CronExpressionBuilder AtDateTime(DateTimeOffset dateTime)
    {
        this.AtTime(dateTime);
        this.DayOfMonth(dateTime.Day);
        this.month = dateTime.Month.ToString();
        this.dayOfWeek = "*";

        return this;
    }

    /// <summary>Sets the expression to run every specified number of seconds.</summary>
    /// <param name="interval">The seconds interval from 1 to 59.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().EverySeconds(15).Build();</code></example>
    public CronExpressionBuilder EverySeconds(int interval)
    {
        if (interval is < 1 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Seconds interval must be between 1 and 59.");
        }

        this.seconds = $"*/{interval}";

        return this;
    }

    /// <summary>Sets the expression to run every specified number of minutes.</summary>
    /// <param name="interval">The minutes interval from 1 to 59.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().EveryMinutes(5).Build();</code></example>
    public CronExpressionBuilder EveryMinutes(int interval)
    {
        if (interval is < 1 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Minutes interval must be between 1 and 59.");
        }

        this.minutes = $"*/{interval}";
        this.hours = "*";

        return this;
    }

    /// <summary>Sets the expression to run every specified number of hours.</summary>
    /// <param name="interval">The hours interval from 1 to 23.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().EveryHours(6).Build();</code></example>
    public CronExpressionBuilder EveryHours(int interval)
    {
        if (interval is < 1 or > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Hours interval must be between 1 and 23.");
        }

        this.minutes = "0";
        this.hours = $"*/{interval}";

        return this;
    }

    /// <summary>Builds the cron expression.</summary>
    /// <returns>The cron expression.</returns>
    /// <example><code>var cron = new CronExpressionBuilder().EveryMinutes(30).Build();</code></example>
    public string Build()
    {
        return string.IsNullOrWhiteSpace(this.seconds)
            ? $"{this.minutes} {this.hours} {this.dayOfMonth} {this.month} {this.dayOfWeek}"
            : $"{this.seconds} {this.minutes} {this.hours} {this.dayOfMonth} {this.month} {this.dayOfWeek}";
    }

    private static void ValidateRange(int start, int end, int min, int max, string startName, string endName, string name)
    {
        if (start < min || start > max)
        {
            throw new ArgumentOutOfRangeException(startName, $"Start {name} must be between {min} and {max}.");
        }

        if (end < start || end > max)
        {
            throw new ArgumentOutOfRangeException(endName, $"End {name} must be between start and {max}.");
        }
    }
}

/// <summary>
/// Days of week for <see cref="CronExpressionBuilder"/>.
/// </summary>
/// <example><code>new CronExpressionBuilder().DayOfWeek(CronDayOfWeek.Monday);</code></example>
public enum CronDayOfWeek
{
    /// <summary>Sunday.</summary>
    Sunday,

    /// <summary>Monday.</summary>
    Monday,

    /// <summary>Tuesday.</summary>
    Tuesday,

    /// <summary>Wednesday.</summary>
    Wednesday,

    /// <summary>Thursday.</summary>
    Thursday,

    /// <summary>Friday.</summary>
    Friday,

    /// <summary>Saturday.</summary>
    Saturday
}

/// <summary>
/// Months for <see cref="CronExpressionBuilder"/>.
/// </summary>
/// <example><code>new CronExpressionBuilder().Month(CronMonth.January);</code></example>
public enum CronMonth
{
    /// <summary>January.</summary>
    January = 1,

    /// <summary>February.</summary>
    February,

    /// <summary>March.</summary>
    March,

    /// <summary>April.</summary>
    April,

    /// <summary>May.</summary>
    May,

    /// <summary>June.</summary>
    June,

    /// <summary>July.</summary>
    July,

    /// <summary>August.</summary>
    August,

    /// <summary>September.</summary>
    September,

    /// <summary>October.</summary>
    October,

    /// <summary>November.</summary>
    November,

    /// <summary>December.</summary>
    December
}
