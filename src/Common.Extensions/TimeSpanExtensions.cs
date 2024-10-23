// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

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

    [DebuggerStepThrough]
    public static TimeSpan TruncateToSeconds(this TimeSpan timeSpan)
    {
        return new TimeSpan(timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
    }

    [DebuggerStepThrough]
    public static TimeSpan ParseTime(this string source)
    {
        var result = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(source))
        {
            return result;
        }

        // Handle compact formats first
        if (source.All(char.IsDigit))
        {
            switch (source.Length)
            {
                case 4: // HHmm
                    source = $"{source[..2]}:{source[2..]}";
                    break;
                case 6: // HHmmss
                    source = $"{source[..2]}:{source[2..4]}:{source[4..]}";
                    break;
            }
        }

        // Define accepted formats
        var formats = new[]
        {
            "HH:mm:ss", // 24-hour with seconds (e.g., "14:30:00")
            "HH:mm", // 24-hour without seconds (e.g., "14:30")
            "hh:mm:ss tt", // 12-hour with seconds (e.g., "02:30:00 PM")
            "hh:mm tt", // 12-hour without seconds (e.g., "02:30 PM")
            "HHmmss", // 24-hour compact with seconds (e.g., "143000")
            "HHmm", // 24-hour compact without seconds (e.g., "1430")
            "hh:mm:ss", // 12-hour with seconds without meridiem (assumes AM)
            "hh:mm" // 12-hour without seconds without meridiem (assumes AM)
        };

        // Try parsing as TimeSpan first
        if (TimeSpan.TryParse(source, out result))
        {
            return result;
        }

        // Try parsing as DateTime with various formats
        if (DateTime.TryParseExact(
                source,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateTime))
        {
            result = dateTime.TimeOfDay;

            return result;
        }

        return result;
    }

    [DebuggerStepThrough]
    public static bool TryParseTime(this string source, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        // Handle compact formats first
        if (source.All(char.IsDigit))
        {
            switch (source.Length)
            {
                case 4: // HHmm
                    source = $"{source[..2]}:{source[2..]}";
                    break;
                case 6: // HHmmss
                    source = $"{source[..2]}:{source[2..4]}:{source[4..]}";
                    break;
            }
        }

        // Define accepted formats
        var formats = new[]
        {
            "HH:mm:ss", // 24-hour with seconds (e.g., "14:30:00")
            "HH:mm", // 24-hour without seconds (e.g., "14:30")
            "hh:mm:ss tt", // 12-hour with seconds (e.g., "02:30:00 PM")
            "hh:mm tt", // 12-hour without seconds (e.g., "02:30 PM")
            "HHmmss", // 24-hour compact with seconds (e.g., "143000")
            "HHmm", // 24-hour compact without seconds (e.g., "1430")
            "hh:mm:ss", // 12-hour with seconds without meridiem (assumes AM)
            "hh:mm" // 12-hour without seconds without meridiem (assumes AM)
        };

        // Try parsing as TimeSpan first
        if (TimeSpan.TryParse(source, out result))
        {
            return true;
        }

        // Try parsing as DateTime with various formats
        if (DateTime.TryParseExact(
                source,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateTime))
        {
            result = dateTime.TimeOfDay;

            return true;
        }

        return false;
    }
}