// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

public static class DateTimeExtensions
{
    [DebuggerStepThrough]
    public static DateTime StartOfDay(this DateTime source)
    {
        return new DateTime(source.Year, source.Month, source.Day, 0, 0, 0, 0, source.Kind);
    }

    [DebuggerStepThrough]
    public static DateTime EndOfDay(this DateTime source)
    {
        return source.StartOfDay().AddDays(1).AddSeconds(-1);
    }

    [DebuggerStepThrough]
    public static DateTime StartOfWeek(this DateTime source)
    {
        return StartOfWeek(source, CultureInfo.InvariantCulture.DateTimeFormat.FirstDayOfWeek);
    }

    [DebuggerStepThrough]
    public static DateTime StartOfWeek(this DateTime source, DayOfWeek day)
    {
        var offset = source.DayOfWeek - day;
        if (offset < 0)
        {
            offset += 7;
        }

        return source.AddDays(-1 * offset);
    }

    [DebuggerStepThrough]
    public static DateTime StartOfMonth(this DateTime source)
    {
        return new DateTime(source.Year, source.Month, 1, 0, 0, 0, 0, source.Kind);
    }

    [DebuggerStepThrough]
    public static DateTime EndOfMonth(this DateTime source)
    {
        return source.StartOfMonth().AddMonths(1).AddSeconds(-1);
    }

    [DebuggerStepThrough]
    public static DateTime StartOfYear(this DateTime source)
    {
        return new DateTime(source.Year, 1, 1, 0, 0, 0, 0, source.Kind);
    }

    [DebuggerStepThrough]
    public static DateTime EndOfYear(this DateTime source)
    {
        return source.StartOfYear().AddYears(1).AddSeconds(-1);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset StartOfDay(this DateTimeOffset source)
    {
        return new DateTimeOffset(source.Year, source.Month, source.Day, 0, 0, 0, 0, source.Offset);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset EndOfDay(this DateTimeOffset source)
    {
        return source.StartOfDay().AddDays(1).AddSeconds(-1);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset StartOfWeek(this DateTimeOffset source)
    {
        return StartOfWeek(source, CultureInfo.InvariantCulture.DateTimeFormat.FirstDayOfWeek);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset StartOfWeek(this DateTimeOffset source, DayOfWeek day)
    {
        var offset = source.DayOfWeek - day;
        if (offset < 0)
        {
            offset += 7;
        }

        return source.AddDays(-1 * offset);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset StartOfMonth(this DateTimeOffset source)
    {
        return new DateTimeOffset(source.Year, source.Month, 1, 0, 0, 0, 0, source.Offset);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset EndOfMonth(this DateTimeOffset source)
    {
        return source.StartOfMonth().AddMonths(1).AddSeconds(-1);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset StartOfYear(this DateTimeOffset source)
    {
        return new DateTimeOffset(source.Year, 1, 1, 0, 0, 0, 0, source.Offset);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset EndOfYear(this DateTimeOffset source)
    {
        return source.StartOfYear().AddYears(1).AddSeconds(-1);
    }
}