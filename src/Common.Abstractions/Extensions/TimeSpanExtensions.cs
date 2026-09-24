// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Provides timeout-token creation, duration comparison, unit construction, truncation, and flexible clock-text parsing.
/// </summary>
public static class TimeSpanExtensions
{
    /// <param name="source">The optional timeout; a missing value produces a source without scheduled cancellation.</param>
    extension(TimeSpan? source)
    {
        /// <summary>Creates a cancellation source from an optional timeout.</summary>
        /// <returns>A new cancellation token source.</returns>
        [DebuggerStepThrough]
        public CancellationTokenSource ToCancellationTokenSource()
        {
            if (source.HasValue)
            {
                return source.Value.ToCancellationTokenSource();
            }

            return new CancellationTokenSource();
        }

        /// <summary>Creates a cancellation source using a fallback timeout when no timeout is supplied.</summary>
        /// <param name="defaultTimeout">The timeout used when <paramref name="source"/> has no value.</param>
        /// <returns>A new cancellation token source.</returns>
        [DebuggerStepThrough]
        public CancellationTokenSource ToCancellationTokenSource(TimeSpan defaultTimeout)
        {
            return (source ?? defaultTimeout).ToCancellationTokenSource();
        }
    }

    /// <param name="source">The first duration.</param>
    extension(TimeSpan source)
    {
        /// <summary>Returns the shorter of two durations by comparing their tick counts.</summary>
        /// <param name="other">The second duration.</param>
        /// <returns>The shorter duration, or <paramref name="source"/> when equal.</returns>
        [DebuggerStepThrough]
        public TimeSpan Min(TimeSpan other)
        {
            return source.Ticks > other.Ticks ? other : source;
        }

        /// <summary>Returns the longer of two durations by comparing their tick counts.</summary>
        /// <param name="other">The second duration.</param>
        /// <returns>The longer duration, or <paramref name="source"/> when equal.</returns>
        [DebuggerStepThrough]
        public TimeSpan Max(TimeSpan other)
        {
            return source.Ticks < other.Ticks ? other : source;
        }

        /// <summary>Creates a cancellation source whose cancellation behavior is determined by a timeout.</summary>
        /// <returns>A new cancellation token source.</returns>
        [DebuggerStepThrough]
        public CancellationTokenSource ToCancellationTokenSource()
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

        /// <summary>Removes fractional seconds while preserving the day, hour, minute, and whole-second components.</summary>
        /// <returns>A duration with zero fractional-second ticks.</returns>
        [DebuggerStepThrough]
        public TimeSpan TruncateToSeconds()
        {
            return new TimeSpan(source.Days, source.Hours, source.Minutes, source.Seconds);
        }
    }

    extension(long value)
    {
        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Ticks</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Ticks()
        {
            return TimeSpan.FromTicks(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Milliseconds</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Milliseconds()
        {
            return TimeSpan.FromMilliseconds(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Seconds</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Seconds()
        {
            return TimeSpan.FromSeconds(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Minutes</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Minutes()
        {
            return TimeSpan.FromMinutes(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Hours</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Hours()
        {
            return TimeSpan.FromHours(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Days</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Days()
        {
            return TimeSpan.FromDays(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Weeks</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Weeks()
        {
            return TimeSpan.FromDays(value * 7);
        }
    }

    extension(int value)
    {
        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Ticks</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Ticks()
        {
            return TimeSpan.FromTicks(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Milliseconds</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Milliseconds()
        {
            return TimeSpan.FromMilliseconds(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Seconds</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Seconds()
        {
            return TimeSpan.FromSeconds(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Minutes</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Minutes()
        {
            return TimeSpan.FromMinutes(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Hours</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Hours()
        {
            return TimeSpan.FromHours(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Days</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Days()
        {
            return TimeSpan.FromDays(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Weeks</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Weeks()
        {
            return TimeSpan.FromDays(value * 7);
        }
    }

    extension(short value)
    {
        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Ticks</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Ticks()
        {
            return TimeSpan.FromTicks(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Milliseconds</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Milliseconds()
        {
            return TimeSpan.FromMilliseconds(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Seconds</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Seconds()
        {
            return TimeSpan.FromSeconds(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Minutes</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Minutes()
        {
            return TimeSpan.FromMinutes(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Hours</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Hours()
        {
            return TimeSpan.FromHours(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Days</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Days()
        {
            return TimeSpan.FromDays(value);
        }

        /// <summary>
        ///     Returns a <see cref="TimeSpan" /> represented by <paramref name="value" /> as <c>Weeks</c>.
        /// </summary>
        [DebuggerStepThrough]
        public TimeSpan Weeks()
        {
            return TimeSpan.FromDays(value * 7);
        }
    }

    /// <param name="source">The text to parse.</param>
    extension(string source)
    {
        /// <summary>Parses duration or clock text, including compact <c>HHmm</c> and <c>HHmmss</c> forms.</summary>
        /// <returns>The parsed duration or time of day; <see cref="TimeSpan.Zero"/> when parsing fails.</returns>
        [DebuggerStepThrough]
        public TimeSpan ParseTime()
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
            }

            return result;
        }

        /// <summary>Attempts to parse duration or clock text, including compact <c>HHmm</c> and <c>HHmmss</c> forms.</summary>
        /// <param name="result">The parsed duration or time of day, or zero when parsing fails.</param>
        /// <returns><see langword="true"/> when either duration or invariant clock parsing succeeds.</returns>
        [DebuggerStepThrough]
        public bool TryParseTime(out TimeSpan result)
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
}
