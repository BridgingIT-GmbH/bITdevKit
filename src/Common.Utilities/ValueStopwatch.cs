// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

/// <summary>
///     A high-performance stopwatch intended for measuring short time intervals.
///     The <see cref="ValueStopwatch" /> is a lightweight, value-type alternative to the <see cref="Stopwatch" /> class.
/// </summary>
/// <remarks>
///     This struct provides high-resolution timing using the underlying system's performance counter.
///     It maintains state in a readonly struct, minimizing heap allocations.
/// </remarks>
public readonly struct ValueStopwatch // no alloc stopwatch
{
    /// <summary>
    ///     The conversion factor from the timestamp's frequency to ticks.
    ///     This value represents the number of ticks per second divided by
    ///     the frequency of the Stopwatch timer.
    /// </summary>
    private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

    /// <summary>
    ///     Represents the starting timestamp of the stopwatch.
    /// </summary>
    /// <remarks>
    ///     This field holds the initial timestamp when the stopwatch was started. A value of 0
    ///     indicates that the stopwatch is not active.
    /// </remarks>
    private readonly long start;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ValueStopwatch" /> struct.
    ///     Represents a lightweight stopwatch which measures elapsed time.
    ///     This struct is immutable and performs better than Stopwatch when used in frequently called operations.
    /// </summary>
    private ValueStopwatch(long start)
    {
        this.start = start;
    }

    /// <summary>
    ///     Indicates whether the stopwatch is active.
    /// </summary>
    /// <returns>
    ///     True if the stopwatch has been started; otherwise, false.
    /// </returns>
    public bool IsActive => this.start != 0;

    /// <summary>
    ///     Starts a new instance of <see cref="ValueStopwatch" /> with the current timestamp.
    /// </summary>
    /// <returns>A new <see cref="ValueStopwatch" /> instance initialized with the current timestamp.</returns>
    public static ValueStopwatch StartNew()
    {
        return new ValueStopwatch(GetTimestamp());
    }

    /// <summary>
    ///     Retrieves the current timestamp in ticks.
    /// </summary>
    /// <returns>
    ///     A long representing the current timestamp in ticks.
    /// </returns>
    public static long GetTimestamp()
    {
        return Stopwatch.GetTimestamp();
    }

    /// <summary>
    ///     Calculates the elapsed time between the start and end timestamps.
    /// </summary>
    /// <param name="start">The starting timestamp.</param>
    /// <param name="end">The ending timestamp.</param>
    /// <returns>The elapsed time as a <see cref="TimeSpan" />.</returns>
    public static TimeSpan GetElapsedTime(long start, long end)
    {
        return new TimeSpan((long)(TimestampToTicks * (end - start)));
    }

    /// <summary>
    ///     Calculates the elapsed time in milliseconds between two timestamps.
    /// </summary>
    /// <param name="start">The starting timestamp.</param>
    /// <param name="end">The ending timestamp.</param>
    /// <return>The elapsed time in milliseconds between the two timestamps.</return>
    public static long GetElapsedMilliseconds(long start, long end)
    {
        return (end - start) * 1000 / Stopwatch.Frequency;
    }

    /// <summary>
    ///     Returns the elapsed time since the start of the stopwatch.
    /// </summary>
    /// <return>
    ///     A <see cref="TimeSpan" /> representing the elapsed time since the stopwatch started.
    /// </return>
    public TimeSpan GetElapsedTime()
    {
        return GetElapsedTime(this.start, GetTimestamp());
    }

    /// <summary>
    ///     Retrieves the elapsed time in milliseconds between two timestamps.
    /// </summary>
    /// <returns>The number of milliseconds elapsed between the start and end timestamps.</returns>
    public long GetElapsedMilliseconds()
    {
        return GetElapsedMilliseconds(this.start, GetTimestamp());
    }
}