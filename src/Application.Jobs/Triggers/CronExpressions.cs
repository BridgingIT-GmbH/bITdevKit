// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Common cron expressions for the Jobs feature.
/// </summary>
/// <example>
/// <code>
/// trigger.Cron(CronExpressions.Every30Minutes);
/// </code>
/// </example>
public static class CronExpressions
{
    /// <summary>A cron expression that never produces an occurrence.</summary>
    public const string Never = "0 0 31 2 *";

    /// <summary>Runs every second. Uses the Jobs six-field seconds format.</summary>
    public const string EverySecond = "*/1 * * * * *";

    /// <summary>Runs every five seconds. Uses the Jobs six-field seconds format.</summary>
    public const string Every5Seconds = "*/5 * * * * *";

    /// <summary>Runs every ten seconds. Uses the Jobs six-field seconds format.</summary>
    public const string Every10Seconds = "*/10 * * * * *";

    /// <summary>Runs every fifteen seconds. Uses the Jobs six-field seconds format.</summary>
    public const string Every15Seconds = "*/15 * * * * *";

    /// <summary>Runs every thirty seconds. Uses the Jobs six-field seconds format.</summary>
    public const string Every30Seconds = "*/30 * * * * *";

    /// <summary>Runs every minute.</summary>
    public const string EveryMinute = "* * * * *";

    /// <summary>Runs every five minutes.</summary>
    public const string Every5Minutes = "*/5 * * * *";

    /// <summary>Runs every ten minutes.</summary>
    public const string Every10Minutes = "*/10 * * * *";

    /// <summary>Runs every fifteen minutes.</summary>
    public const string Every15Minutes = "*/15 * * * *";

    /// <summary>Runs every thirty minutes.</summary>
    public const string Every30Minutes = "*/30 * * * *";

    /// <summary>Runs every hour.</summary>
    public const string EveryHour = "0 * * * *";

    /// <summary>Runs every two hours.</summary>
    public const string EveryTwoHours = "0 */2 * * *";

    /// <summary>Runs every six hours.</summary>
    public const string EverySixHours = "0 */6 * * *";

    /// <summary>Runs every nine hours.</summary>
    public const string EveryNineHours = "0 */9 * * *";

    /// <summary>Runs every twelve hours.</summary>
    public const string EveryTwelveHours = "0 */12 * * *";

    /// <summary>Runs daily at midnight.</summary>
    public const string DailyAtMidnight = "0 0 * * *";

    /// <summary>Runs daily at noon.</summary>
    public const string DailyAtNoon = "0 12 * * *";

    /// <summary>Runs daily at midnight and noon.</summary>
    public const string DailyAtMidnightAndNoon = "0 0,12 * * *";

    /// <summary>Runs daily at 1 AM.</summary>
    public const string DailyAt1AM = "0 1 * * *";

    /// <summary>Runs daily at 2 AM.</summary>
    public const string DailyAt2AM = "0 2 * * *";

    /// <summary>Runs daily at 3 AM.</summary>
    public const string DailyAt3AM = "0 3 * * *";

    /// <summary>Runs daily at 4 AM.</summary>
    public const string DailyAt4AM = "0 4 * * *";

    /// <summary>Runs daily at 5 AM.</summary>
    public const string DailyAt5AM = "0 5 * * *";

    /// <summary>Runs daily at 6 AM.</summary>
    public const string DailyAt6AM = "0 6 * * *";

    /// <summary>Runs daily at 7 AM.</summary>
    public const string DailyAt7AM = "0 7 * * *";

    /// <summary>Runs daily at 8 AM.</summary>
    public const string DailyAt8AM = "0 8 * * *";

    /// <summary>Runs daily at 9 AM.</summary>
    public const string DailyAt9AM = "0 9 * * *";

    /// <summary>Runs daily at 10 AM.</summary>
    public const string DailyAt10AM = "0 10 * * *";

    /// <summary>Runs daily at 11 AM.</summary>
    public const string DailyAt11AM = "0 11 * * *";

    /// <summary>Runs daily at 1 PM.</summary>
    public const string DailyAt1PM = "0 13 * * *";

    /// <summary>Runs daily at 2 PM.</summary>
    public const string DailyAt2PM = "0 14 * * *";

    /// <summary>Runs daily at 3 PM.</summary>
    public const string DailyAt3PM = "0 15 * * *";

    /// <summary>Runs daily at 4 PM.</summary>
    public const string DailyAt4PM = "0 16 * * *";

    /// <summary>Runs daily at 5 PM.</summary>
    public const string DailyAt5PM = "0 17 * * *";

    /// <summary>Runs daily at 6 PM.</summary>
    public const string DailyAt6PM = "0 18 * * *";

    /// <summary>Runs daily at 7 PM.</summary>
    public const string DailyAt7PM = "0 19 * * *";

    /// <summary>Runs daily at 8 PM.</summary>
    public const string DailyAt8PM = "0 20 * * *";

    /// <summary>Runs daily at 9 PM.</summary>
    public const string DailyAt9PM = "0 21 * * *";

    /// <summary>Runs daily at 10 PM.</summary>
    public const string DailyAt10PM = "0 22 * * *";

    /// <summary>Runs daily at 11 PM.</summary>
    public const string DailyAt11PM = "0 23 * * *";

    /// <summary>Runs weekly on Sunday at midnight.</summary>
    public const string WeeklyOnSundayAtMidnight = "0 0 * * SUN";

    /// <summary>Runs weekly on Monday at midnight.</summary>
    public const string WeeklyOnMondayAtMidnight = "0 0 * * MON";

    /// <summary>Runs weekly on Tuesday at midnight.</summary>
    public const string WeeklyOnTuesdayAtMidnight = "0 0 * * TUE";

    /// <summary>Runs weekly on Wednesday at midnight.</summary>
    public const string WeeklyOnWednesdayAtMidnight = "0 0 * * WED";

    /// <summary>Runs weekly on Thursday at midnight.</summary>
    public const string WeeklyOnThursdayAtMidnight = "0 0 * * THU";

    /// <summary>Runs weekly on Friday at midnight.</summary>
    public const string WeeklyOnFridayAtMidnight = "0 0 * * FRI";

    /// <summary>Runs weekly on Saturday at midnight.</summary>
    public const string WeeklyOnSaturdayAtMidnight = "0 0 * * SAT";

    /// <summary>Runs monthly at midnight on the first day.</summary>
    public const string MonthlyAtMidnightOnFirstDay = "0 0 1 * *";

    /// <summary>Runs monthly at midnight on the last day.</summary>
    public const string MonthlyAtMidnightOnLastDay = "0 0 L * *";
}
