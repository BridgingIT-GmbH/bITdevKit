// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application;

public struct CronExpressions
// https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html#example-cron-expressions
// http://www.cronmaker.com/?1
{
    public const string Never = "0 0 0 ? * * 2099";

    public const string EverySecond = "0/1 * * * * ?";

    public const string Every5Seconds = "0/5 * * * * ?";

    public const string Every10Seconds = "0/10 * * * * ?";

    public const string Every15Seconds = "0/15 * * * * ?";

    public const string Every30Seconds = "0/30 * * * * ?";

    public const string EveryMinute = "0 0/1 * * * ?";

    public const string Every5Minutes = "0 0/5 * * * ?";

    public const string Every10Minutes = "0 0/10 * * * ?";

    public const string Every15Minutes = "0 0/15 * * * ?";

    public const string Every30Minutes = "0 0/30 * * * ?";

    public const string EveryHour = "0 0 * * * ?";

    public const string EveryTwoHours = "0 0 */2 * * ?";

    public const string EverySixHours = "0 0 */6 * * ?";

    public const string EveryNineHours = "0 0 */9 * * ?";

    public const string EveryTwelveHours = "0 0 */12 * * ?";

    public const string DailyAtMidnight = "0 0 0 * * ?";

    public const string DailyAtNoon = "0 0 12 * * ?";

    public const string DailyAtMidnightAndNoon = "0 0 0,12 * * ?";

    public const string DailyAt1AM = "0 0 1 * * ?";

    public const string DailyAt2AM = "0 0 2 * * ?";

    public const string DailyAt3AM = "0 0 3 * * ?";

    public const string DailyAt4AM = "0 0 4 * * ?";

    public const string DailyAt5AM = "0 0 5 * * ?";

    public const string DailyAt6AM = "0 0 6 * * ?";

    public const string DailyAt7AM = "0 0 7 * * ?";

    public const string DailyAt8AM = "0 0 8 * * ?";

    public const string DailyAt9AM = "0 0 9 * * ?";

    public const string DailyAt10AM = "0 0 10 * * ?";

    public const string DailyAt11AM = "0 0 11 * * ?";

    public const string DailyAt1PM = "0 0 13 * * ?";

    public const string DailyAt2PM = "0 0 14 * * ?";

    public const string DailyAt3PM = "0 0 15 * * ?";

    public const string DailyAt4PM = "0 0 16 * * ?";

    public const string DailyAt5PM = "0 0 17 * * ?";

    public const string DailyAt6PM = "0 0 18 * * ?";

    public const string DailyAt7PM = "0 0 19 * * ?";

    public const string DailyAt8PM = "0 0 20 * * ?";

    public const string DailyAt9PM = "0 0 21 * * ?";

    public const string DailyAt10PM = "0 0 22 * * ?";

    public const string DailyAt11PM = "0 0 23 * * ?";

    public const string WeeklyOnSundayAtMidnight = "0 0 0 * * SUN";

    public const string WeeklyOnMondayAtMidnight = "0 0 0 * * MON";

    public const string WeeklyOnTuesdayAtMidnight = "0 0 0 * * TUE";

    public const string WeeklyOnWednesdayAtMidnight = "0 0 0 * * WED";

    public const string WeeklyOnThursdayAtMidnight = "0 0 0 * * THU";

    public const string WeeklyOnFridayAtMidnight = "0 0 0 * * FRI";

    public const string WeeklyOnSaturdayAtMidnight = "0 0 0 * * SAT";

    public const string MonthlyAtMidnightOnFirstDay = "0 0 0 1 * ?";

    public const string MonthlyAtMidnightOnLastDay = "0 0 0 L * ?";
}