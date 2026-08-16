// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Provides clock arithmetic, same-day range checks, boundary alignment, and invariant formatting for <see cref="TimeOnly"/> values.
/// </summary>
public static class TimeOnlyExtensions
{
    /// <summary>Adds a signed clock-unit amount, wrapping across midnight according to <see cref="TimeOnly"/> arithmetic.</summary>
    /// <param name="time">The time to adjust.</param>
    /// <param name="unit">The clock unit to add.</param>
    /// <param name="amount">The signed number of units.</param>
    /// <returns>The adjusted time of day.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="unit"/> is unsupported.</exception>
    [DebuggerStepThrough]
    public static TimeOnly Add(this TimeOnly time, TimeUnit unit, int amount)
    {
        return unit switch
        {
            TimeUnit.Millisecond => time.Add(TimeSpan.FromMilliseconds(amount)),
            TimeUnit.Second => time.Add(TimeSpan.FromSeconds(amount)),
            TimeUnit.Minute => time.AddMinutes(amount),
            TimeUnit.Hour => time.AddHours(amount),
            TimeUnit.Day => time.Add(TimeSpan.FromDays(amount)),
            _ => throw new ArgumentException("Unsupported TimeUnit.", nameof(unit))
        };
    }

    /// <summary>Determines whether a time lies between two same-day boundaries.</summary>
    /// <param name="time">The time to evaluate.</param>
    /// <param name="start">The lower same-day boundary.</param>
    /// <param name="end">The upper same-day boundary.</param>
    /// <param name="inclusive">Whether equality with either boundary counts as in range.</param>
    /// <returns><see langword="true"/> when the selected boundary comparison succeeds.</returns>
    [DebuggerStepThrough]
    public static bool IsInRange(this TimeOnly time, TimeOnly start, TimeOnly end, bool inclusive = true)
    {
        return inclusive ? time >= start && time <= end : time > start && time < end;
    }

    /// <summary>Determines whether a time is within a past or future same-day range relative to the current local time.</summary>
    /// <param name="time">The time to evaluate.</param>
    /// <param name="unit">The clock unit used to calculate the boundary.</param>
    /// <param name="amount">The number of units between now and the boundary.</param>
    /// <param name="direction">Whether the range extends into the past or future.</param>
    /// <param name="inclusive">Whether now and the calculated boundary are included.</param>
    /// <returns><see langword="true"/> when the time lies in the same-day range.</returns>
    [DebuggerStepThrough]
    public static bool IsInRelativeRange(this TimeOnly time, TimeUnit unit, int amount, DateTimeDirection direction, bool inclusive = true)
    {
        var now = TimeOnly.FromDateTime(DateTime.Now);
        return time.IsInRelativeRange(now, unit, amount, direction, inclusive);
    }

    /// <summary>
    /// Determines whether a time is inside a relative same-day range around an explicit reference time.
    /// </summary>
    /// <param name="time">The time to evaluate.</param>
    /// <param name="reference">The same-day reference time.</param>
    /// <param name="unit">The relative unit.</param>
    /// <param name="amount">The amount of units.</param>
    /// <param name="direction">The direction from the reference time.</param>
    /// <param name="inclusive">Whether the boundaries are included.</param>
    /// <returns><c>true</c> when the time is inside the range; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// <para>This method uses same-day semantics and does not infer midnight crossing.</para>
    /// <example>
    /// <code>
    /// var isSoon = target.IsInRelativeRange(reference, TimeUnit.Minute, 5, DateTimeDirection.Future);
    /// </code>
    /// </example>
    /// </remarks>
    [DebuggerStepThrough]
    public static bool IsInRelativeRange(this TimeOnly time, TimeOnly reference, TimeUnit unit, int amount, DateTimeDirection direction, bool inclusive = true)
    {
        var referenceTime = direction == DateTimeDirection.Past
            ? reference.Add(unit, -amount)
            : reference.Add(unit, amount);

        return direction == DateTimeDirection.Past
            ? (inclusive ? time <= reference && time >= referenceTime : time < reference && time > referenceTime)
            : (inclusive ? time >= reference && time <= referenceTime : time > reference && time < referenceTime);
    }

    /// <summary>Floors a time to the start of its containing millisecond, second, minute, hour, or day.</summary>
    /// <param name="timeOnly">The time to floor.</param>
    /// <param name="timeUnit">The clock boundary to use.</param>
    /// <returns>The aligned time.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="timeUnit"/> is unsupported.</exception>
    [DebuggerStepThrough]
    public static TimeOnly FloorTo(this TimeOnly timeOnly, TimeUnit timeUnit)
    {
        switch (timeUnit)
        {
            case TimeUnit.Millisecond:
                return TimeOnly.FromTimeSpan(TimeSpan.FromTicks(timeOnly.Ticks - (timeOnly.Ticks % TimeSpan.TicksPerMillisecond)));
            case TimeUnit.Second:
                return TimeOnly.FromTimeSpan(TimeSpan.FromTicks(timeOnly.Ticks - (timeOnly.Ticks % TimeSpan.TicksPerSecond)));
            case TimeUnit.Minute:
                return new TimeOnly(timeOnly.Hour, timeOnly.Minute, 0);
            case TimeUnit.Hour:
                return new TimeOnly(timeOnly.Hour, 0, 0);
            case TimeUnit.Day:
                return TimeOnly.MinValue;
            default:
                throw new ArgumentException("Unsupported TimeUnit.", nameof(timeUnit));
        }
    }

    /// <summary>
    /// Floors a <see cref="TimeOnly"/> to an arbitrary positive interval.
    /// </summary>
    /// <param name="timeOnly">The source value.</param>
    /// <param name="interval">The positive interval.</param>
    /// <returns>The floored value.</returns>
    /// <remarks><example><code>var value = time.FloorTo(TimeSpan.FromMinutes(15));</code></example></remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is zero or negative.</exception>
    [DebuggerStepThrough]
    public static TimeOnly FloorTo(this TimeOnly timeOnly, TimeSpan interval)
    {
        EnsurePositiveInterval(interval);

        return TimeOnly.FromTimeSpan(TimeSpan.FromTicks(timeOnly.Ticks - (timeOnly.Ticks % interval.Ticks)));
    }

    /// <summary>Moves a time to the next clock-unit boundary unless it is already aligned.</summary>
    /// <param name="timeOnly">The time to ceiling.</param>
    /// <param name="timeUnit">The clock boundary to use.</param>
    /// <returns>The aligned time; a day ceiling wraps to midnight.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="timeUnit"/> is unsupported.</exception>
    [DebuggerStepThrough]
    public static TimeOnly CeilingTo(this TimeOnly timeOnly, TimeUnit timeUnit)
    {
        var floor = timeOnly.FloorTo(timeUnit);
        if (floor == timeOnly)
        {
            return timeOnly;
        }

        return timeUnit switch
        {
            TimeUnit.Millisecond => floor.Add(TimeSpan.FromMilliseconds(1)),
            TimeUnit.Second => floor.Add(TimeSpan.FromSeconds(1)),
            TimeUnit.Minute => floor.AddMinutes(1),
            TimeUnit.Hour => floor.AddHours(1),
            TimeUnit.Day => TimeOnly.MinValue,
            _ => throw new ArgumentException("Unsupported TimeUnit.", nameof(timeUnit))
        };
    }

    /// <summary>
    /// Ceilings a <see cref="TimeOnly"/> to an arbitrary positive interval.
    /// </summary>
    /// <param name="timeOnly">The source value.</param>
    /// <param name="interval">The positive interval.</param>
    /// <returns>The ceiling value.</returns>
    /// <remarks><example><code>var value = time.CeilingTo(TimeSpan.FromMinutes(15));</code></example></remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is zero or negative.</exception>
    [DebuggerStepThrough]
    public static TimeOnly CeilingTo(this TimeOnly timeOnly, TimeSpan interval)
    {
        EnsurePositiveInterval(interval);

        var floor = timeOnly.FloorTo(interval);
        return floor == timeOnly ? timeOnly : floor.Add(interval);
    }

    /// <summary>Rounds a time to the nearest clock unit using half-up tick arithmetic and wraps at midnight.</summary>
    /// <param name="timeOnly">The time to round.</param>
    /// <param name="timeUnit">The clock unit used as the rounding interval.</param>
    /// <returns>The rounded time of day.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="timeUnit"/> is unsupported.</exception>
    [DebuggerStepThrough]
    public static TimeOnly RoundToNearest(this TimeOnly timeOnly, TimeUnit timeUnit)
    {
        var ticks = timeUnit switch
        {
            TimeUnit.Millisecond => TimeSpan.TicksPerMillisecond,
            TimeUnit.Second => TimeSpan.TicksPerSecond,
            TimeUnit.Minute => TimeSpan.TicksPerMinute,
            TimeUnit.Hour => TimeSpan.TicksPerHour,
            TimeUnit.Day => TimeSpan.TicksPerDay,
            _ => throw new ArgumentException("Unsupported TimeUnit.", nameof(timeUnit))
        };

        var roundedTicks = ((timeOnly.Ticks + (ticks / 2)) / ticks) * ticks;
        roundedTicks %= TimeSpan.TicksPerDay;
        return TimeOnly.FromTimeSpan(TimeSpan.FromTicks(roundedTicks));
    }

    /// <summary>
    /// Rounds a <see cref="TimeOnly"/> to the nearest arbitrary positive interval.
    /// </summary>
    /// <param name="timeOnly">The source value.</param>
    /// <param name="interval">The positive interval.</param>
    /// <returns>The rounded value.</returns>
    /// <remarks><example><code>var value = time.RoundToNearest(TimeSpan.FromMinutes(15));</code></example></remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is zero or negative.</exception>
    [DebuggerStepThrough]
    public static TimeOnly RoundToNearest(this TimeOnly timeOnly, TimeSpan interval)
    {
        EnsurePositiveInterval(interval);

        var roundedTicks = ((timeOnly.Ticks + (interval.Ticks / 2)) / interval.Ticks) * interval.Ticks;
        roundedTicks %= TimeSpan.TicksPerDay;
        return TimeOnly.FromTimeSpan(TimeSpan.FromTicks(roundedTicks));
    }

    /// <summary>
    /// Formats a time using ISO HH:mm:ss format.
    /// </summary>
    /// <param name="source">The time to format.</param>
    /// <returns>The invariant ISO time string.</returns>
    /// <remarks>
    /// <example>
    /// <code>
    /// var text = new TimeOnly(13, 45, 30).ToIsoTimeString();
    /// </code>
    /// </example>
    /// </remarks>
    [DebuggerStepThrough]
    public static string ToIsoTimeString(this TimeOnly source)
    {
        return source.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static void EnsurePositiveInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
        }
    }
}
