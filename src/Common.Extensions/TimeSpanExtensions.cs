// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static class TimeSpanExtensions
{
    [DebuggerStepThrough]
    public static CancellationTokenSource ToCancellationTokenSource(this TimeSpan source)
    {
        if (source == TimeSpan.Zero)
        {
            var result = new CancellationTokenSource();
            result.Cancel();
            return result;
        }

        if (source.Ticks > 0)
        {
            return new CancellationTokenSource(source);
        }

        return new CancellationTokenSource();
    }

    [DebuggerStepThrough]
    public static CancellationTokenSource ToCancellationTokenSource(this TimeSpan? source)
    {
        if (source.HasValue)
        {
            return source.Value.ToCancellationTokenSource();
        }

        return new CancellationTokenSource();
    }

    [DebuggerStepThrough]
    public static CancellationTokenSource ToCancellationTokenSource(this TimeSpan? source, TimeSpan defaultTimeout)
    {
        return (source ?? defaultTimeout).ToCancellationTokenSource();
    }

    [DebuggerStepThrough]
    public static TimeSpan Min(this TimeSpan source, TimeSpan other)
    {
        return source.Ticks > other.Ticks ? other : source;
    }

    [DebuggerStepThrough]
    public static TimeSpan Max(this TimeSpan source, TimeSpan other)
    {
        return source.Ticks < other.Ticks ? other : source;
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Ticks</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Ticks(this long value)
    {
        return TimeSpan.FromTicks(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Milliseconds</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Milliseconds(this long value)
    {
        return TimeSpan.FromMilliseconds(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Seconds</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Seconds(this long value)
    {
        return TimeSpan.FromSeconds(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Minutes</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Minutes(this long value)
    {
        return TimeSpan.FromMinutes(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Hours</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Hours(this long value)
    {
        return TimeSpan.FromHours(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Days</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Days(this long value)
    {
        return TimeSpan.FromDays(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Weeks</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Weeks(this long value)
    {
        return TimeSpan.FromDays(value * 7);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Ticks</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Ticks(this int value)
    {
        return TimeSpan.FromTicks(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Milliseconds</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Milliseconds(this int value)
    {
        return TimeSpan.FromMilliseconds(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Seconds</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Seconds(this int value)
    {
        return TimeSpan.FromSeconds(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Minutes</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Minutes(this int value)
    {
        return TimeSpan.FromMinutes(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Hours</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Hours(this int value)
    {
        return TimeSpan.FromHours(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Days</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Days(this int value)
    {
        return TimeSpan.FromDays(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Weeks</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Weeks(this int value)
    {
        return TimeSpan.FromDays(value * 7);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Ticks</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Ticks(this short value)
    {
        return TimeSpan.FromTicks(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Milliseconds</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Milliseconds(this short value)
    {
        return TimeSpan.FromMilliseconds(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Seconds</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Seconds(this short value)
    {
        return TimeSpan.FromSeconds(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Minutes</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Minutes(this short value)
    {
        return TimeSpan.FromMinutes(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Hours</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Hours(this short value)
    {
        return TimeSpan.FromHours(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Days</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Days(this short value)
    {
        return TimeSpan.FromDays(value);
    }

    /// <summary>
    ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Weeks</c>.
    /// </summary>
    [DebuggerStepThrough]
    public static TimeSpan Weeks(this short value)
    {
        return TimeSpan.FromDays(value * 7);
    }
}