// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Diagnostics;

public static class DateTimeOffsetExtensions
{
    /// <summary>
    /// Gets the start of the day for the specified DateTimeOffset.
    /// </summary>
    /// <param name="source">The DateTimeOffset to get the start of the day for.</param>
    /// <returns>A DateTimeOffset representing the start of the day.</returns>
    [DebuggerStepThrough]
    public static DateTimeOffset StartOfDay(this DateTimeOffset source)
    {
        return new DateTimeOffset(source.Year, source.Month, source.Day, 0, 0, 0, 0, source.Offset);
    }

    /// <summary>
    /// Gets the end of the day for the specified DateTimeOffset.
    /// </summary>
    /// <param name="source">The DateTimeOffset to get the end of the day for.</param>
    /// <returns>A DateTimeOffset representing the end of the day.</returns>
    [DebuggerStepThrough]
    public static DateTimeOffset EndOfDay(this DateTimeOffset source)
    {
        return source.StartOfDay().AddDays(1).AddSeconds(-1);
    }

    /// <summary>
    /// Gets the start of the week for the specified DateTimeOffset with a defined first day of the week.
    /// </summary>
    /// <param name="source">The DateTimeOffset to get the start of the week for.</param>
    /// <param name="day">The first day of the week.</param>
    /// <returns>A DateTimeOffset representing the start of the week.</returns>
    [DebuggerStepThrough]
    public static DateTimeOffset StartOfWeek(this DateTimeOffset source, DayOfWeek day = DayOfWeek.Monday)
    {
        var offset = source.DayOfWeek - day;
        if (offset < 0)
        {
            offset += 7;
        }

        return source.AddDays(-1 * offset).StartOfDay();
    }

    /// <summary>
    /// Gets the end of the week for the specified DateTimeOffset.
    /// </summary>
    /// <param name="source">The DateTimeOffset to get the end of the week for.</param>
    /// <returns>A DateTimeOffset representing the end of the week.</returns>
    [DebuggerStepThrough]
    public static DateTimeOffset EndOfWeek(this DateTimeOffset source)
    {
        return source.StartOfWeek().AddDays(7).AddSeconds(-1);
    }

    /// <summary>
    /// Gets the start of the month for the specified DateTimeOffset.
    /// </summary>
    /// <param name="source">The DateTimeOffset to get the start of the month for.</param>
    /// <returns>A DateTimeOffset representing the start of the month.</returns>
    [DebuggerStepThrough]
    public static DateTimeOffset StartOfMonth(this DateTimeOffset source)
    {
        return new DateTimeOffset(source.Year, source.Month, 1, 0, 0, 0, 0, source.Offset);
    }

    /// <summary>
    /// Gets the end of the month for the specified DateTimeOffset.
    /// </summary>
    /// <param name="source">The DateTimeOffset to get the end of the month for.</param>
    /// <returns>A DateTimeOffset representing the end of the month.</returns>
    [DebuggerStepThrough]
    public static DateTimeOffset EndOfMonth(this DateTimeOffset source)
    {
        return source.StartOfMonth().AddMonths(1).AddSeconds(-1);
    }

    /// <summary>
    /// Gets the start of the year for the specified DateTimeOffset.
    /// </summary>
    /// <param name="source">The DateTimeOffset to get the start of the year for.</param>
    /// <returns>A DateTimeOffset representing the start of the year.</returns>
    [DebuggerStepThrough]
    public static DateTimeOffset StartOfYear(this DateTimeOffset source)
    {
        return new DateTimeOffset(source.Year, 1, 1, 0, 0, 0, 0, source.Offset);
    }

    /// <summary>
    /// Gets the end of the year for the specified DateTimeOffset.
    /// </summary>
    /// <param name="source">The DateTimeOffset to get the end of the year for.</param>
    /// <returns>A DateTimeOffset representing the end of the year.</returns>
    [DebuggerStepThrough]
    public static DateTimeOffset EndOfYear(this DateTimeOffset source)
    {
        return source.StartOfYear().AddYears(1).AddSeconds(-1);
    }

    /// <summary>
    /// Converts a <see cref="DateTimeOffset"/> value to a <see cref="DateOnly"/>, preserving only the calendar date (in the source's local time) and discarding the time and offset components.
    /// </summary>
    /// <param name="source">The source <see cref="DateTimeOffset"/> value.</param>
    /// <returns>A <see cref="DateOnly"/> representing the date portion of the original <see cref="DateTimeOffset"/>.</returns>
    [DebuggerStepThrough]
    public static DateOnly ToDateOnly(this DateTimeOffset source)
    {
        return DateOnly.FromDateTime(source.LocalDateTime);
    }

    /// <summary>
    /// Converts a <see cref="DateTimeOffset"/> value to a <see cref="TimeOnly"/>, preserving only the time-of-day component in the source's local time and discarding the date and offset components.
    /// </summary>
    /// <param name="source">The source <see cref="DateTimeOffset"/> value.</param>
    /// <returns>A <see cref="TimeOnly"/> representing the time portion of the original <see cref="DateTimeOffset"/>.</returns>
    [DebuggerStepThrough]
    public static TimeOnly ToTimeOnly(this DateTimeOffset source)
    {
        return TimeOnly.FromDateTime(source.LocalDateTime);
    }
}