// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

public static class TimeOnlyExtensions
{
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

    [DebuggerStepThrough]
    public static bool IsInRange(this TimeOnly time, TimeOnly start, TimeOnly end, bool inclusive = true)
    {
        return inclusive ? time >= start && time <= end : time > start && time < end;
    }

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
