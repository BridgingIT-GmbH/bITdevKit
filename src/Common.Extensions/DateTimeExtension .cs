// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Diagnostics;
using System.Globalization;

public static class DateTimeExtensions
{
    [DebuggerStepThrough]
    public static DateTime StartOfDay(this DateTime source) =>
        new(source.Year, source.Month, source.Day, 0, 0, 0, 0, source.Kind);

    [DebuggerStepThrough]
    public static DateTime EndOfDay(this DateTime source) =>
        source.StartOfDay().AddDays(1).AddSeconds(-1);

    [DebuggerStepThrough]
    public static DateTime StartOfWeek(this DateTime source) =>
        StartOfWeek(source, CultureInfo.InvariantCulture.DateTimeFormat.FirstDayOfWeek);

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
    public static DateTime StartOfMonth(this DateTime source) =>
        new(source.Year, source.Month, 1, 0, 0, 0, 0, source.Kind);

    [DebuggerStepThrough]
    public static DateTime EndOfMonth(this DateTime source) =>
        source.StartOfMonth().AddMonths(1).AddSeconds(-1);

    [DebuggerStepThrough]
    public static DateTime StartOfYear(this DateTime source) =>
        new(source.Year, 1, 1, 0, 0, 0, 0, source.Kind);

    [DebuggerStepThrough]
    public static DateTime EndOfYear(this DateTime source) =>
        source.StartOfYear().AddYears(1).AddSeconds(-1);

    [DebuggerStepThrough]
    public static DateTimeOffset StartOfDay(this DateTimeOffset source) =>
        new(source.Year, source.Month, source.Day, 0, 0, 0, 0, source.Offset);

    [DebuggerStepThrough]
    public static DateTimeOffset EndOfDay(this DateTimeOffset source) =>
        source.StartOfDay().AddDays(1).AddSeconds(-1);

    [DebuggerStepThrough]
    public static DateTimeOffset StartOfWeek(this DateTimeOffset source) =>
        StartOfWeek(source, CultureInfo.InvariantCulture.DateTimeFormat.FirstDayOfWeek);

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
    public static DateTimeOffset StartOfMonth(this DateTimeOffset source) =>
        new(source.Year, source.Month, 1, 0, 0, 0, 0, source.Offset);

    [DebuggerStepThrough]
    public static DateTimeOffset EndOfMonth(this DateTimeOffset source) =>
        source.StartOfMonth().AddMonths(1).AddSeconds(-1);

    [DebuggerStepThrough]
    public static DateTimeOffset StartOfYear(this DateTimeOffset source) =>
        new(source.Year, 1, 1, 0, 0, 0, 0, source.Offset);

    [DebuggerStepThrough]
    public static DateTimeOffset EndOfYear(this DateTimeOffset source) =>
        source.StartOfYear().AddYears(1).AddSeconds(-1);
}