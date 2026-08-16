// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application;

/// <summary>
/// Represents cron expressions.
/// </summary>
public struct CronExpressions
// https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html#example-cron-expressions
// http://www.cronmaker.com/?1
{
    /// <summary>
    /// Defines the never value.
    /// </summary>
    public const string Never = "0 0 0 ? * * 2099";

    /// <summary>
    /// Defines the every second value.
    /// </summary>
    public const string EverySecond = "0/1 * * * * ?";

    /// <summary>
    /// Defines the every5 seconds value.
    /// </summary>
    public const string Every5Seconds = "0/5 * * * * ?";

    /// <summary>
    /// Defines the every10 seconds value.
    /// </summary>
    public const string Every10Seconds = "0/10 * * * * ?";

    /// <summary>
    /// Defines the every15 seconds value.
    /// </summary>
    public const string Every15Seconds = "0/15 * * * * ?";

    /// <summary>
    /// Defines the every30 seconds value.
    /// </summary>
    public const string Every30Seconds = "0/30 * * * * ?";

    /// <summary>
    /// Defines the every minute value.
    /// </summary>
    public const string EveryMinute = "0 0/1 * * * ?";

    /// <summary>
    /// Defines the every5 minutes value.
    /// </summary>
    public const string Every5Minutes = "0 0/5 * * * ?";

    /// <summary>
    /// Defines the every10 minutes value.
    /// </summary>
    public const string Every10Minutes = "0 0/10 * * * ?";

    /// <summary>
    /// Defines the every15 minutes value.
    /// </summary>
    public const string Every15Minutes = "0 0/15 * * * ?";

    /// <summary>
    /// Defines the every30 minutes value.
    /// </summary>
    public const string Every30Minutes = "0 0/30 * * * ?";

    /// <summary>
    /// Defines the every hour value.
    /// </summary>
    public const string EveryHour = "0 0 * * * ?";

    /// <summary>
    /// Defines the every two hours value.
    /// </summary>
    public const string EveryTwoHours = "0 0 */2 * * ?";

    /// <summary>
    /// Defines the every six hours value.
    /// </summary>
    public const string EverySixHours = "0 0 */6 * * ?";

    /// <summary>
    /// Defines the every nine hours value.
    /// </summary>
    public const string EveryNineHours = "0 0 */9 * * ?";

    /// <summary>
    /// Defines the every twelve hours value.
    /// </summary>
    public const string EveryTwelveHours = "0 0 */12 * * ?";

    /// <summary>
    /// Defines the daily at midnight value.
    /// </summary>
    public const string DailyAtMidnight = "0 0 0 * * ?";

    /// <summary>
    /// Defines the daily at noon value.
    /// </summary>
    public const string DailyAtNoon = "0 0 12 * * ?";

    /// <summary>
    /// Defines the daily at midnight and noon value.
    /// </summary>
    public const string DailyAtMidnightAndNoon = "0 0 0,12 * * ?";

    /// <summary>
    /// Defines the daily at1 am value.
    /// </summary>
    public const string DailyAt1AM = "0 0 1 * * ?";

    /// <summary>
    /// Defines the daily at2 am value.
    /// </summary>
    public const string DailyAt2AM = "0 0 2 * * ?";

    /// <summary>
    /// Defines the daily at3 am value.
    /// </summary>
    public const string DailyAt3AM = "0 0 3 * * ?";

    /// <summary>
    /// Defines the daily at4 am value.
    /// </summary>
    public const string DailyAt4AM = "0 0 4 * * ?";

    /// <summary>
    /// Defines the daily at5 am value.
    /// </summary>
    public const string DailyAt5AM = "0 0 5 * * ?";

    /// <summary>
    /// Defines the daily at6 am value.
    /// </summary>
    public const string DailyAt6AM = "0 0 6 * * ?";

    /// <summary>
    /// Defines the daily at7 am value.
    /// </summary>
    public const string DailyAt7AM = "0 0 7 * * ?";

    /// <summary>
    /// Defines the daily at8 am value.
    /// </summary>
    public const string DailyAt8AM = "0 0 8 * * ?";

    /// <summary>
    /// Defines the daily at9 am value.
    /// </summary>
    public const string DailyAt9AM = "0 0 9 * * ?";

    /// <summary>
    /// Defines the daily at10 am value.
    /// </summary>
    public const string DailyAt10AM = "0 0 10 * * ?";

    /// <summary>
    /// Defines the daily at11 am value.
    /// </summary>
    public const string DailyAt11AM = "0 0 11 * * ?";

    /// <summary>
    /// Defines the daily at1 pm value.
    /// </summary>
    public const string DailyAt1PM = "0 0 13 * * ?";

    /// <summary>
    /// Defines the daily at2 pm value.
    /// </summary>
    public const string DailyAt2PM = "0 0 14 * * ?";

    /// <summary>
    /// Defines the daily at3 pm value.
    /// </summary>
    public const string DailyAt3PM = "0 0 15 * * ?";

    /// <summary>
    /// Defines the daily at4 pm value.
    /// </summary>
    public const string DailyAt4PM = "0 0 16 * * ?";

    /// <summary>
    /// Defines the daily at5 pm value.
    /// </summary>
    public const string DailyAt5PM = "0 0 17 * * ?";

    /// <summary>
    /// Defines the daily at6 pm value.
    /// </summary>
    public const string DailyAt6PM = "0 0 18 * * ?";

    /// <summary>
    /// Defines the daily at7 pm value.
    /// </summary>
    public const string DailyAt7PM = "0 0 19 * * ?";

    /// <summary>
    /// Defines the daily at8 pm value.
    /// </summary>
    public const string DailyAt8PM = "0 0 20 * * ?";

    /// <summary>
    /// Defines the daily at9 pm value.
    /// </summary>
    public const string DailyAt9PM = "0 0 21 * * ?";

    /// <summary>
    /// Defines the daily at10 pm value.
    /// </summary>
    public const string DailyAt10PM = "0 0 22 * * ?";

    /// <summary>
    /// Defines the daily at11 pm value.
    /// </summary>
    public const string DailyAt11PM = "0 0 23 * * ?";

    /// <summary>
    /// Defines the weekly on sunday at midnight value.
    /// </summary>
    public const string WeeklyOnSundayAtMidnight = "0 0 0 * * SUN";

    /// <summary>
    /// Defines the weekly on monday at midnight value.
    /// </summary>
    public const string WeeklyOnMondayAtMidnight = "0 0 0 * * MON";

    /// <summary>
    /// Defines the weekly on tuesday at midnight value.
    /// </summary>
    public const string WeeklyOnTuesdayAtMidnight = "0 0 0 * * TUE";

    /// <summary>
    /// Defines the weekly on wednesday at midnight value.
    /// </summary>
    public const string WeeklyOnWednesdayAtMidnight = "0 0 0 * * WED";

    /// <summary>
    /// Defines the weekly on thursday at midnight value.
    /// </summary>
    public const string WeeklyOnThursdayAtMidnight = "0 0 0 * * THU";

    /// <summary>
    /// Defines the weekly on friday at midnight value.
    /// </summary>
    public const string WeeklyOnFridayAtMidnight = "0 0 0 * * FRI";

    /// <summary>
    /// Defines the weekly on saturday at midnight value.
    /// </summary>
    public const string WeeklyOnSaturdayAtMidnight = "0 0 0 * * SAT";

    /// <summary>
    /// Defines the monthly at midnight on first day value.
    /// </summary>
    public const string MonthlyAtMidnightOnFirstDay = "0 0 0 1 * ?";

    /// <summary>
    /// Defines the monthly at midnight on last day value.
    /// </summary>
    public const string MonthlyAtMidnightOnLastDay = "0 0 0 L * ?";
}
