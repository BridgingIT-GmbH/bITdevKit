// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Provides calendar-boundary, component-extraction, and invariant-formatting helpers for <see cref="DateTimeOffset"/> values.
/// </summary>
public static class DateTimeOffsetExtensions
{
    /// <param name="source">The DateTimeOffset to get the start of the day for.</param>
    extension(DateTimeOffset source)
    {
        /// <summary>
        /// Gets the start of the day for the specified DateTimeOffset.
        /// </summary>
        /// <returns>A DateTimeOffset representing the start of the day.</returns>
        [DebuggerStepThrough]
        public DateTimeOffset StartOfDay()
        {
            return new DateTimeOffset(source.Year, source.Month, source.Day, 0, 0, 0, 0, source.Offset);
        }

        /// <summary>
        /// Gets the end of the day for the specified DateTimeOffset.
        /// </summary>
        /// <returns>A DateTimeOffset representing the end of the day.</returns>
        [DebuggerStepThrough]
        public DateTimeOffset EndOfDay()
        {
            return source.StartOfDay().AddDays(1).AddTicks(-1);
        }

        /// <summary>
        /// Gets the start of the week for the specified DateTimeOffset with a defined first day of the week.
        /// </summary>
        /// <param name="day">The first day of the week.</param>
        /// <returns>A DateTimeOffset representing the start of the week.</returns>
        [DebuggerStepThrough]
        public DateTimeOffset StartOfWeek(DayOfWeek day = DayOfWeek.Monday)
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
        /// <returns>A DateTimeOffset representing the end of the week.</returns>
        [DebuggerStepThrough]
        public DateTimeOffset EndOfWeek()
        {
            return source.StartOfWeek().AddDays(7).AddTicks(-1);
        }

        /// <summary>
        /// Gets the start of the month for the specified DateTimeOffset.
        /// </summary>
        /// <returns>A DateTimeOffset representing the start of the month.</returns>
        [DebuggerStepThrough]
        public DateTimeOffset StartOfMonth()
        {
            return new DateTimeOffset(source.Year, source.Month, 1, 0, 0, 0, 0, source.Offset);
        }

        /// <summary>
        /// Gets the end of the month for the specified DateTimeOffset.
        /// </summary>
        /// <returns>A DateTimeOffset representing the end of the month.</returns>
        [DebuggerStepThrough]
        public DateTimeOffset EndOfMonth()
        {
            return source.StartOfMonth().AddMonths(1).AddTicks(-1);
        }

        /// <summary>
        /// Gets the start of the year for the specified DateTimeOffset.
        /// </summary>
        /// <returns>A DateTimeOffset representing the start of the year.</returns>
        [DebuggerStepThrough]
        public DateTimeOffset StartOfYear()
        {
            return new DateTimeOffset(source.Year, 1, 1, 0, 0, 0, 0, source.Offset);
        }

        /// <summary>
        /// Gets the end of the year for the specified DateTimeOffset.
        /// </summary>
        /// <returns>A DateTimeOffset representing the end of the year.</returns>
        [DebuggerStepThrough]
        public DateTimeOffset EndOfYear()
        {
            return source.StartOfYear().AddYears(1).AddTicks(-1);
        }

        /// <summary>
        /// Converts a <see cref="DateTimeOffset"/> value to a <see cref="DateOnly"/>, preserving only the calendar date (in the source's local time) and discarding the time and offset components.
        /// </summary>
        /// <returns>A <see cref="DateOnly"/> representing the date portion of the original <see cref="DateTimeOffset"/>.</returns>
        [DebuggerStepThrough]
        public DateOnly ToDateOnly()
        {
            return source.ToOffsetDateOnly();
        }

        /// <summary>
        /// Converts a <see cref="DateTimeOffset"/> value to a <see cref="TimeOnly"/>, preserving only the time-of-day component in the source's local time and discarding the date and offset components.
        /// </summary>
        /// <returns>A <see cref="TimeOnly"/> representing the time portion of the original <see cref="DateTimeOffset"/>.</returns>
        [DebuggerStepThrough]
        public TimeOnly ToTimeOnly()
        {
            return source.ToOffsetTimeOnly();
        }

        /// <summary>
        /// Extracts the date in the value's own offset-clock time.
        /// </summary>
        /// <returns>The offset-clock date.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var date = instant.ToOffsetDateOnly();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public DateOnly ToOffsetDateOnly()
        {
            return DateOnly.FromDateTime(source.DateTime);
        }

        /// <summary>
        /// Extracts the time in the value's own offset-clock time.
        /// </summary>
        /// <returns>The offset-clock time.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var time = instant.ToOffsetTimeOnly();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public TimeOnly ToOffsetTimeOnly()
        {
            return TimeOnly.FromDateTime(source.DateTime);
        }

        /// <summary>
        /// Extracts the date after converting the instant to UTC.
        /// </summary>
        /// <returns>The UTC date.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var utcDate = instant.ToUtcDateOnly();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public DateOnly ToUtcDateOnly()
        {
            return DateOnly.FromDateTime(source.UtcDateTime);
        }

        /// <summary>
        /// Extracts the time after converting the instant to UTC.
        /// </summary>
        /// <returns>The UTC time.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var utcTime = instant.ToUtcTimeOnly();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public TimeOnly ToUtcTimeOnly()
        {
            return TimeOnly.FromDateTime(source.UtcDateTime);
        }

        /// <summary>
        /// Formats a <see cref="DateTimeOffset"/> as an ISO string with its offset.
        /// </summary>
        /// <returns>An invariant ISO offset timestamp.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var text = instant.ToIsoOffsetString();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public string ToIsoOffsetString()
        {
            return source.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats a <see cref="DateTimeOffset"/> as a file-safe UTC timestamp.
        /// </summary>
        /// <returns>An invariant timestamp such as 20260629T134530Z.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var stamp = instant.ToFileSafeTimestamp();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public string ToFileSafeTimestamp()
        {
            return source.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        }
    }
}
