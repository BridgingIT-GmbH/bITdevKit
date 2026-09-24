// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Provides deterministic parsing, calendar arithmetic, boundary alignment, and epoch conversion for <see cref="DateTime"/> values.
/// </summary>
public static class DateTimeExtensions
{
    /// <param name="source">The DateTime to get the start of the day for.</param>
    extension(DateTime source)
    {
        /// <summary>
        /// Gets the start of the day for the specified DateTime.
        /// </summary>
        /// <returns>A DateTime representing the start of the day.</returns>
        [DebuggerStepThrough]
        public DateTime StartOfDay()
        {
            return new DateTime(source.Year, source.Month, source.Day, 0, 0, 0, 0, source.Kind);
        }

        /// <summary>
        /// Gets the end of the day for the specified DateTime.
        /// </summary>
        /// <returns>A DateTime representing the end of the day.</returns>
        [DebuggerStepThrough]
        public DateTime EndOfDay()
        {
            return source.StartOfDay().AddDays(1).AddTicks(-1);
        }

        /// <summary>
        /// Gets the start of the week for the specified DateTime with a defined first day of the week.
        /// </summary>
        /// <param name="day">The first day of the week.</param>
        /// <returns>A DateTime representing the start of the week.</returns>
        [DebuggerStepThrough]
        public DateTime StartOfWeek(DayOfWeek day = DayOfWeek.Monday)
        {
            var offset = source.DayOfWeek - day;
            if (offset < 0)
            {
                offset += 7;
            }

            return source.AddDays(-1 * offset).StartOfDay();
        }

        /// <summary>
        /// Gets the end of the week for the specified DateTime.
        /// </summary>
        /// <returns>A DateTime representing the end of the week.</returns>
        [DebuggerStepThrough]
        public DateTime EndOfWeek()
        {
            return source.StartOfWeek().AddDays(7).AddTicks(-1);
        }

        /// <summary>
        /// Gets the start of the month for the specified DateTime.
        /// </summary>
        /// <returns>A DateTime representing the start of the month.</returns>
        [DebuggerStepThrough]
        public DateTime StartOfMonth()
        {
            return new DateTime(source.Year, source.Month, 1, 0, 0, 0, 0, source.Kind);
        }

        /// <summary>
        /// Gets the end of the month for the specified DateTime.
        /// </summary>
        /// <returns>A DateTime representing the end of the month.</returns>
        [DebuggerStepThrough]
        public DateTime EndOfMonth()
        {
            return source.StartOfMonth().AddMonths(1).AddTicks(-1);
        }

        /// <summary>
        /// Gets the start of the year for the specified DateTime.
        /// </summary>
        /// <returns>A DateTime representing the start of the year.</returns>
        [DebuggerStepThrough]
        public DateTime StartOfYear()
        {
            return new DateTime(source.Year, 1, 1, 0, 0, 0, 0, source.Kind);
        }

        /// <summary>
        /// Gets the end of the year for the specified DateTime.
        /// </summary>
        /// <returns>A DateTime representing the end of the year.</returns>
        [DebuggerStepThrough]
        public DateTime EndOfYear()
        {
            return source.StartOfYear().AddYears(1).AddTicks(-1);
        }

        /// <summary>
        /// Returns a UTC <see cref="DateTime"/> for values that are already UTC or have no kind.
        /// </summary>
        /// <returns>A UTC <see cref="DateTime"/> with the same clock value.</returns>
        /// <remarks>
        /// <para>Local values are rejected to avoid hiding a machine-local conversion.</para>
        /// <example>
        /// <code>
        /// var stored = new DateTime(2026, 1, 1);
        /// var utc = stored.AssumeUtc();
        /// </code>
        /// </example>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is local.</exception>
        [DebuggerStepThrough]
        public DateTime AssumeUtc()
        {
            return source.Kind switch
            {
                DateTimeKind.Utc => source,
                DateTimeKind.Unspecified => DateTime.SpecifyKind(source, DateTimeKind.Utc),
                DateTimeKind.Local => throw new ArgumentException("Local DateTime cannot be assumed to be UTC.", nameof(source)),
                _ => source
            };
        }

        /// <summary>
        /// Ensures a <see cref="DateTime"/> is usable as UTC.
        /// </summary>
        /// <returns>A UTC <see cref="DateTime"/> with the same clock value.</returns>
        /// <remarks>
        /// <para>Unspecified values are interpreted as UTC. Local values are rejected.</para>
        /// <example>
        /// <code>
        /// var utc = value.EnsureUtc();
        /// </code>
        /// </example>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is local.</exception>
        [DebuggerStepThrough]
        public DateTime EnsureUtc()
        {
            return source.Kind switch
            {
                DateTimeKind.Utc => source,
                DateTimeKind.Unspecified => DateTime.SpecifyKind(source, DateTimeKind.Utc),
                DateTimeKind.Local => throw new ArgumentException("DateTime must be UTC or unspecified UTC.", nameof(source)),
                _ => source
            };
        }

        /// <summary>
        /// Determines whether a value has <see cref="DateTimeKind.Utc"/>.
        /// </summary>
        /// <returns><c>true</c> when the value is UTC; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// if (createdAt.IsUtc()) { }
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public bool IsUtc()
        {
            return source.Kind == DateTimeKind.Utc;
        }

        /// <summary>
        /// Determines whether a value has <see cref="DateTimeKind.Unspecified"/>.
        /// </summary>
        /// <returns><c>true</c> when the value has no kind; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// if (storedAt.IsUnspecified()) { }
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public bool IsUnspecified()
        {
            return source.Kind == DateTimeKind.Unspecified;
        }
    }

    /// <param name="source">The date or epoch text to parse.</param>
    extension(string source)
    {
        /// <summary>
        /// Parses a date string or Unix epoch value using the default policy that rejects ambiguous slash dates.
        /// </summary>
        /// <returns>The parsed UTC value, or <see langword="null"/> when parsing fails.</returns>
        [DebuggerStepThrough]
        public DateTime? ParseDateOrEpoch()
        {
            return source.TryParseDateOrEpoch(out var result) ? result : null;
        }

        /// <summary>
        /// Parses a date string or Unix epoch value into a UTC <see cref="DateTime"/>.
        /// </summary>
        /// <param name="ambiguousDatePolicy">The policy used for ambiguous slash dates.</param>
        /// <returns>A UTC <see cref="DateTime"/> when parsing succeeds; otherwise <c>null</c>.</returns>
        /// <remarks>
        /// <para>Epoch seconds and milliseconds return UTC. Date strings without offsets are interpreted as UTC.</para>
        /// <example>
        /// <code>
        /// var parsed = "1735689600000".ParseDateOrEpoch();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public DateTime? ParseDateOrEpoch(AmbiguousDatePolicy ambiguousDatePolicy)
        {
            return source.TryParseDateOrEpoch(out var result, ambiguousDatePolicy) ? result : null;
        }

        /// <summary>
        /// Parses a date string or Unix epoch value into a UTC <see cref="DateTime"/> or throws.
        /// </summary>
        /// <returns>The parsed UTC value.</returns>
        /// <remarks>
        /// <para>Ambiguous slash dates are rejected by default.</para>
        /// <example>
        /// <code>
        /// var parsed = "2026-01-01".ParseDateOrEpochOrThrow();
        /// </code>
        /// </example>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the source cannot be parsed.</exception>
        [DebuggerStepThrough]
        public DateTime ParseDateOrEpochOrThrow()
        {
            return source.ParseDateOrEpochOrThrow(AmbiguousDatePolicy.Reject);
        }

        /// <summary>
        /// Parses a date string or Unix epoch value into a UTC <see cref="DateTime"/> or throws.
        /// </summary>
        /// <param name="ambiguousDatePolicy">The policy used for ambiguous slash dates.</param>
        /// <returns>The parsed UTC value.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var parsed = "03/04/2026".ParseDateOrEpochOrThrow(AmbiguousDatePolicy.PreferDayMonthYear);
        /// </code>
        /// </example>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the source cannot be parsed.</exception>
        [DebuggerStepThrough]
        public DateTime ParseDateOrEpochOrThrow(AmbiguousDatePolicy ambiguousDatePolicy)
        {
            return source.TryParseDateOrEpoch(out var result, ambiguousDatePolicy)
                ? result
                : throw new ArgumentException($"Invalid date format: {source}. Use a supported date format or Unix epoch.", nameof(source));
        }

        /// <summary>
        /// Attempts to parse a date string or Unix epoch value using the default policy that rejects ambiguous slash dates.
        /// </summary>
        /// <param name="result">The parsed UTC value when successful; otherwise, the default value.</param>
        /// <returns><see langword="true"/> when parsing succeeds.</returns>
        [DebuggerStepThrough]
        public bool TryParseDateOrEpoch(out DateTime result)
        {
            return source.TryParseDateOrEpoch(out result, AmbiguousDatePolicy.Reject);
        }

        /// <summary>
        /// Attempts to parse a date string or Unix epoch value into a UTC <see cref="DateTime"/>.
        /// </summary>
        /// <param name="result">The parsed UTC value when parsing succeeds.</param>
        /// <param name="ambiguousDatePolicy">The policy used for ambiguous slash dates.</param>
        /// <returns><c>true</c> when parsing succeeds; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// <para>Epoch milliseconds are detected by 13 or more digits. Slash dates such as 03/04/2026 are rejected unless a policy is provided.</para>
        /// <example>
        /// <code>
        /// if ("1735689600".TryParseDateOrEpoch(out var parsed)) { }
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public bool TryParseDateOrEpoch(out DateTime result, AmbiguousDatePolicy ambiguousDatePolicy)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            source = source.Trim();

            // Try parsing as Unix epoch seconds or milliseconds. The threshold avoids clashing with yyyyMMdd.
            if (long.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch) &&
                epoch is > 99999999 or < 0)
            {
                try
                {
                    var digitCount = source.TrimStart('-').Length;
                    result = digitCount >= 13
                        ? DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime
                        : DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;

                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }
            }

            if (IsAmbiguousSlashDate(source))
            {
                if (ambiguousDatePolicy == AmbiguousDatePolicy.Reject)
                {
                    return false;
                }

                var preferredFormat = ambiguousDatePolicy == AmbiguousDatePolicy.PreferDayMonthYear ? "dd/MM/yyyy" : "MM/dd/yyyy";
                if (DateTime.TryParseExact(
                        source,
                        preferredFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var preferredDate))
                {
                    result = DateTime.SpecifyKind(preferredDate, DateTimeKind.Utc);
                    return true;
                }
            }

            var formats = new[]
            {
                "yyyy-MM-dd",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss.fffffff",
                "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
                "dd/MM/yyyy",
                "MM/dd/yyyy",
                "dd-MM-yyyy",
                "dd.MM.yyyy",
                "yyyyMMdd",
                "dd MMM yyyy",
                "d MMMM yyyy"
            };

            // Exact known formats: Values without an explicit offset are interpreted as UTC by convention.
            if (DateTime.TryParseExact(
                    source,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var exactDate))
            {
                result = exactDate.Kind switch
                {
                    DateTimeKind.Utc => exactDate,
                    DateTimeKind.Local => exactDate.ToUniversalTime(),
                    _ => DateTime.SpecifyKind(exactDate, DateTimeKind.Utc)
                };

                return true;
            }

            // Fallback: Use DateTimeOffset so strings with offsets are adjusted to UTC deterministically.
            if (DateTimeOffset.TryParse(
                    source,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsedOffset))
            {
                result = parsedOffset.UtcDateTime;
                return true;
            }

            return false;
        }
    }

    private static bool IsAmbiguousSlashDate(string source)
    {
        var parts = source.Split('/');
        return parts.Length == 3 &&
            int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var first) &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var second) &&
            first is >= 1 and <= 12 &&
            second is >= 1 and <= 12;
    }

    /// <param name="date">The value to adjust.</param>
    extension(DateTime date)
    {
        /// <summary>
        /// Adds signed calendar days, seven-day weeks, months, or years while preserving the time and <see cref="DateTime.Kind"/>.
        /// </summary>
        /// <param name="unit">The calendar unit to add.</param>
        /// <param name="amount">The signed number of units.</param>
        /// <returns>The adjusted value, clamping the day when the target month is shorter.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="unit"/> is unsupported.</exception>
        [DebuggerStepThrough]
        public DateTime Add(DateUnit unit, int amount)
        {
            return unit switch
            {
                DateUnit.Day => date.AddDays(amount),
                DateUnit.Week => date.AddDays(7 * amount),
                DateUnit.Month => AddMonths(date, amount),
                DateUnit.Year => AddMonths(date, amount * 12),
                _ => throw new ArgumentException("Unsupported DateUnit.", nameof(unit))
            };
        }

        /// <summary>
        /// Adds a time-unit amount to a <see cref="DateTime"/>.
        /// </summary>
        /// <param name="unit">The unit to add.</param>
        /// <param name="amount">The amount to add.</param>
        /// <returns>The adjusted value.</returns>
        /// <remarks><example><code>var later = now.Add(TimeUnit.Second, 30);</code></example></remarks>
        [DebuggerStepThrough]
        public DateTime Add(TimeUnit unit, int amount)
        {
            return unit switch
            {
                TimeUnit.Millisecond => date.AddMilliseconds(amount),
                TimeUnit.Second => date.AddSeconds(amount),
                TimeUnit.Minute => date.AddMinutes(amount),
                TimeUnit.Hour => date.AddHours(amount),
                TimeUnit.Day => date.AddDays(amount),
                _ => throw new ArgumentException("Unsupported TimeUnit.", nameof(unit))
            };
        }
    }

    [DebuggerStepThrough]
    private static DateTime AddMonths(DateTime date, int months)
    {
        // Calculate the target year and month
        var totalMonths = date.Year * 12 + (date.Month - 1) + months;
        var targetYear = totalMonths / 12;
        var targetMonth = totalMonths % 12 + 1;

        // Determine the last day of the target month to avoid invalid dates
        var daysInTargetMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        var targetDay = Math.Min(date.Day, daysInTargetMonth);

        return new DateTime(targetYear, targetMonth, targetDay, date.Hour, date.Minute, date.Second, date.Millisecond, date.Kind);
    }

    /// <param name="date">The value to evaluate.</param>
    extension(DateTime date)
    {
        /// <summary>
        /// Determines whether a date-time lies between two boundaries.
        /// </summary>
        /// <param name="start">The lower boundary.</param>
        /// <param name="end">The upper boundary.</param>
        /// <param name="inclusive">Whether equality with either boundary counts as in range.</param>
        /// <returns><see langword="true"/> when the selected boundary comparison succeeds.</returns>
        [DebuggerStepThrough]
        public bool IsInRange(DateTime start, DateTime end, bool inclusive = true)
        {
            return inclusive ? date >= start && date <= end : date > start && date < end;
        }

        /// <summary>
        /// Determines whether a given date is within a specified relative range from the current date.
        /// </summary>
        /// <param name="unit">The unit of time for the range (e.g., Days, Weeks, Months, Years).</param>
        /// <param name="amount">The amount of the unit to define the range.</param>
        /// <param name="direction">The direction relative to the current date (Past or Future).</param>
        /// <param name="inclusive">Specifies whether the range should be inclusive of the boundary dates.</param>
        /// <returns>True if the date is within the specified relative range; otherwise, false.</returns>
        [DebuggerStepThrough]
        public bool IsInRelativeRange(DateUnit unit, int amount, DateTimeDirection direction, bool inclusive = true)
        {
            var now = GetNowForKind(date.Kind);
            return date.IsInRelativeRange(now, unit, amount, direction, inclusive);
        }

        /// <summary>
        /// Determines whether a date is inside a relative range around an explicit reference value.
        /// </summary>
        /// <param name="reference">The reference point.</param>
        /// <param name="unit">The relative unit.</param>
        /// <param name="amount">The amount of units.</param>
        /// <param name="direction">The direction from the reference point.</param>
        /// <param name="inclusive">Whether the boundaries are included.</param>
        /// <returns><c>true</c> when the date is inside the range; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var reference = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        /// var isSoon = reference.AddDays(3).IsInRelativeRange(reference, DateUnit.Day, 5, DateTimeDirection.Future);
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public bool IsInRelativeRange(DateTime reference, DateUnit unit, int amount, DateTimeDirection direction, bool inclusive = true)
        {
            var referenceDate = direction == DateTimeDirection.Past ? reference.Add(unit, -amount) : reference.Add(unit, amount);

            return direction == DateTimeDirection.Past
                ? (inclusive ? date <= reference && date >= referenceDate : date < reference && date > referenceDate)
                : (inclusive ? date >= reference && date <= referenceDate : date > reference && date < referenceDate);
        }

        /// <summary>
        /// Determines whether a date is inside a relative range around a <see cref="TimeProvider"/> reference value.
        /// </summary>
        /// <param name="timeProvider">The provider used to obtain the reference time.</param>
        /// <param name="unit">The relative unit.</param>
        /// <param name="amount">The amount of units.</param>
        /// <param name="direction">The direction from the reference point.</param>
        /// <param name="inclusive">Whether the boundaries are included.</param>
        /// <returns><c>true</c> when the date is inside the range; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var isRecent = value.IsInRelativeRange(TimeProvider.System, DateUnit.Day, 7, DateTimeDirection.Past);
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public bool IsInRelativeRange(TimeProvider timeProvider, DateUnit unit, int amount, DateTimeDirection direction, bool inclusive = true)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);

            return date.IsInRelativeRange(GetNowForKind(date.Kind, timeProvider), unit, amount, direction, inclusive);
        }

        /// <summary>Gets the invariant-culture week number using Monday as the first day and the first-day calendar rule.</summary>
        /// <returns>The calendar week number.</returns>
        [DebuggerStepThrough]
        public int GetWeekOfYear()
        {
            var cultureInfo = CultureInfo.InvariantCulture;
            var calendar = cultureInfo.Calendar;

            return calendar.GetWeekOfYear(date, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
        }

        /// <summary>Determines whether the value belongs to a leap year in the Gregorian calendar.</summary>
        /// <returns><see langword="true"/> when the year is a leap year.</returns>
        [DebuggerStepThrough]
        public bool IsLeapYear()
        {
            return DateTime.IsLeapYear(date.Year);
        }

        /// <summary>
        /// Calculates the signed whole-day difference from the current time of the same <see cref="DateTime.Kind"/> to a target value.
        /// </summary>
        /// <returns>The truncated whole-day difference.</returns>
        [DebuggerStepThrough]
        public int DaysUntil()
        {
            var now = GetNowForKind(date.Kind);
            return date.DaysUntil(now);
        }

        /// <summary>
        /// Calculates whole days from an explicit reference value to a target value.
        /// </summary>
        /// <param name="reference">The reference date.</param>
        /// <returns>The whole day difference.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var days = dueDate.DaysUntil(reference);
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public int DaysUntil(DateTime reference)
        {
            return (date - reference).Days;
        }

        /// <summary>
        /// Calculates whole days from a <see cref="TimeProvider"/> reference value to a target value.
        /// </summary>
        /// <param name="timeProvider">The provider used to obtain the reference time.</param>
        /// <returns>The whole day difference.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var days = dueDate.DaysUntil(TimeProvider.System);
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public int DaysUntil(TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);

            return date.DaysUntil(GetNowForKind(date.Kind, timeProvider));
        }

        /// <summary>Converts a date-time to Unix epoch seconds using its kind-aware offset conversion.</summary>
        /// <returns>The number of seconds since the Unix epoch.</returns>
        [DebuggerStepThrough]
        public long ToUnixTimeSeconds()
        {
            return date.ToDateTimeOffset().ToUnixTimeSeconds();
        }

        /// <summary>Converts a date-time to Unix epoch milliseconds using its kind-aware offset conversion.</summary>
        /// <returns>The number of milliseconds since the Unix epoch.</returns>
        [DebuggerStepThrough]
        public long ToUnixTimeMilliseconds()
        {
            return date.ToDateTimeOffset().ToUnixTimeMilliseconds();
        }
    }

    /// <param name="source">The number of seconds since 1970-01-01T00:00:00Z.</param>
    extension(long source)
    {
        /// <summary>
        /// Converts Unix epoch seconds to a UTC <see cref="DateTime"/>.
        /// </summary>
        /// <returns>A UTC <see cref="DateTime"/>.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var utc = 1735689600L.FromUnixTimeSecondsToUtcDateTime();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public DateTime FromUnixTimeSecondsToUtcDateTime()
        {
            return DateTimeOffset.FromUnixTimeSeconds(source).UtcDateTime;
        }

        /// <summary>
        /// Converts Unix epoch milliseconds to a UTC <see cref="DateTime"/>.
        /// </summary>
        /// <returns>A UTC <see cref="DateTime"/>.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var utc = 1735689600000L.FromUnixTimeMillisecondsToUtcDateTime();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public DateTime FromUnixTimeMillisecondsToUtcDateTime()
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(source).UtcDateTime;
        }

        /// <summary>
        /// Converts Unix epoch seconds to a <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <returns>A UTC <see cref="DateTimeOffset"/>.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var instant = 1735689600L.FromUnixTimeSecondsToDateTimeOffset();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public DateTimeOffset FromUnixTimeSecondsToDateTimeOffset()
        {
            return DateTimeOffset.FromUnixTimeSeconds(source);
        }

        /// <summary>
        /// Converts Unix epoch milliseconds to a <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <returns>A UTC <see cref="DateTimeOffset"/>.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var instant = 1735689600000L.FromUnixTimeMillisecondsToDateTimeOffset();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public DateTimeOffset FromUnixTimeMillisecondsToDateTimeOffset()
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(source);
        }
    }

    /// <param name="date">The value to convert.</param>
    extension(DateTime date)
    {
        /// <summary>
        /// Converts a date-time to an offset value while preserving its represented instant when the kind is UTC or local.
        /// </summary>
        /// <param name="offset">The requested offset, or the natural UTC/local offset when omitted.</param>
        /// <returns>The corresponding offset value; unspecified values are interpreted at the supplied offset or UTC.</returns>
        [DebuggerStepThrough]
        public DateTimeOffset ToDateTimeOffset(TimeSpan? offset = null)
        {
            return date.Kind switch
            {
                DateTimeKind.Utc => offset is null ? new DateTimeOffset(date, TimeSpan.Zero) : new DateTimeOffset(date, TimeSpan.Zero).ToOffset(offset.Value),
                DateTimeKind.Local when offset is null => new DateTimeOffset(date),
                DateTimeKind.Local => new DateTimeOffset(date.ToUniversalTime(), TimeSpan.Zero).ToOffset(offset.Value),
                _ => new DateTimeOffset(date, offset ?? TimeSpan.Zero)
            };
        }

        /// <summary>Calculates the signed duration from one date-time to another.</summary>
        /// <param name="target">The ending value.</param>
        /// <returns><paramref name="target"/> minus <paramref name="date"/>.</returns>
        [DebuggerStepThrough]
        public TimeSpan TimeSpanTo(DateTime target)
        {
            return target - date;
        }

        /// <summary>
        /// Converts a <see cref="DateTime"/> value to a <see cref="DateOnly"/> preserving the calendar date and discarding the time component.
        /// </summary>
        /// <returns>A <see cref="DateOnly"/> representing the date portion of the original <see cref="DateTime"/>.</returns>
        [DebuggerStepThrough]
        public DateOnly ToDateOnly()
        {
            return DateOnly.FromDateTime(date);
        }

        /// <summary>
        /// Converts a <see cref="DateTime"/> value to a <see cref="TimeOnly"/> preserving only the time-of-day component and discarding the date component.
        /// </summary>
        /// <returns>A <see cref="TimeOnly"/> representing the time portion of the original <see cref="DateTime"/>.</returns>
        [DebuggerStepThrough]
        public TimeOnly ToTimeOnly()
        {
            return TimeOnly.FromDateTime(date);
        }

        /// <summary>Floors a date-time to the start of its containing day, Monday-based week, month, or year.</summary>
        /// <param name="dateUnit">The calendar boundary to use.</param>
        /// <returns>The start of the containing calendar unit with the original kind.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="dateUnit"/> is unsupported.</exception>
        [DebuggerStepThrough]
        public DateTime FloorTo(DateUnit dateUnit)
        {
            switch (dateUnit)
            {
                case DateUnit.Day:
                    return date.StartOfDay();
                case DateUnit.Week:
                    return date.StartOfWeek();
                case DateUnit.Month:
                    return date.StartOfMonth();
                case DateUnit.Year:
                    return date.StartOfYear();
                default:
                    throw new ArgumentException("Unsupported DateUnit.", nameof(dateUnit));
            }
        }

        /// <summary>Floors a date-time to the start of its containing millisecond, second, minute, hour, or day.</summary>
        /// <param name="timeUnit">The clock boundary to use.</param>
        /// <returns>The aligned value with the original kind.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="timeUnit"/> is unsupported.</exception>
        [DebuggerStepThrough]
        public DateTime FloorTo(TimeUnit timeUnit)
        {
            switch (timeUnit)
            {
                case TimeUnit.Millisecond:
                    return new DateTime(date.Ticks - (date.Ticks % TimeSpan.TicksPerMillisecond), date.Kind);
                case TimeUnit.Second:
                    return new DateTime(date.Ticks - (date.Ticks % TimeSpan.TicksPerSecond), date.Kind);
                case TimeUnit.Minute:
                    return new DateTime(date.Ticks - (date.Ticks % TimeSpan.TicksPerMinute), date.Kind);
                case TimeUnit.Hour:
                    return new DateTime(date.Ticks - (date.Ticks % TimeSpan.TicksPerHour), date.Kind);
                case TimeUnit.Day:
                    return date.StartOfDay();
                default:
                    throw new ArgumentException("Unsupported TimeUnit.", nameof(timeUnit));
            }
        }

        /// <summary>
        /// Floors a <see cref="DateTime"/> to an arbitrary positive interval.
        /// </summary>
        /// <param name="interval">The positive interval.</param>
        /// <returns>The floored value.</returns>
        /// <remarks><example><code>var value = timestamp.FloorTo(TimeSpan.FromMinutes(15));</code></example></remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is zero or negative.</exception>
        [DebuggerStepThrough]
        public DateTime FloorTo(TimeSpan interval)
        {
            EnsurePositiveInterval(interval);

            return new DateTime(date.Ticks - (date.Ticks % interval.Ticks), date.Kind);
        }

        /// <summary>Moves a date-time to the next calendar boundary unless it is already aligned.</summary>
        /// <param name="dateUnit">The day, Monday-based week, month, or year boundary to use.</param>
        /// <returns>The source when aligned; otherwise, the next calendar boundary.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="dateUnit"/> is unsupported.</exception>
        [DebuggerStepThrough]
        public DateTime CeilingTo(DateUnit dateUnit)
        {
            var floor = date.FloorTo(dateUnit);
            if (floor == date)
            {
                return date;
            }

            return dateUnit switch
            {
                DateUnit.Day => floor.AddDays(1),
                DateUnit.Week => floor.AddDays(7),
                DateUnit.Month => floor.AddMonths(1),
                DateUnit.Year => floor.AddYears(1),
                _ => throw new ArgumentException("Unsupported DateUnit.", nameof(dateUnit))
            };
        }

        /// <summary>Moves a date-time to the next clock-unit boundary unless it is already aligned.</summary>
        /// <param name="timeUnit">The millisecond, second, minute, hour, or day boundary to use.</param>
        /// <returns>The source when aligned; otherwise, the next clock boundary.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="timeUnit"/> is unsupported.</exception>
        [DebuggerStepThrough]
        public DateTime CeilingTo(TimeUnit timeUnit)
        {
            var floor = date.FloorTo(timeUnit);
            if (floor == date)
            {
                return date;
            }

            return timeUnit switch
            {
                TimeUnit.Millisecond => floor.AddMilliseconds(1),
                TimeUnit.Second => floor.AddSeconds(1),
                TimeUnit.Minute => floor.AddMinutes(1),
                TimeUnit.Hour => floor.AddHours(1),
                TimeUnit.Day => floor.AddDays(1),
                _ => throw new ArgumentException("Unsupported TimeUnit.", nameof(timeUnit))
            };
        }

        /// <summary>
        /// Ceilings a <see cref="DateTime"/> to an arbitrary positive interval.
        /// </summary>
        /// <param name="interval">The positive interval.</param>
        /// <returns>The ceiling value.</returns>
        /// <remarks><example><code>var value = timestamp.CeilingTo(TimeSpan.FromMinutes(15));</code></example></remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is zero or negative.</exception>
        [DebuggerStepThrough]
        public DateTime CeilingTo(TimeSpan interval)
        {
            EnsurePositiveInterval(interval);

            var floor = date.FloorTo(interval);
            return floor == date ? date : floor.AddTicks(interval.Ticks);
        }

        /// <summary>Aligns a date-time to the start of its containing calendar unit.</summary>
        /// <param name="dateUnit">The calendar unit whose lower boundary should be returned.</param>
        /// <returns>The same value as <see cref="FloorTo(DateTime, DateUnit)"/>.</returns>
        [DebuggerStepThrough]
        public DateTime RoundToNearest(DateUnit dateUnit)
        {
            return date.FloorTo(dateUnit);
        }

        /// <summary>Rounds a date-time to the nearest millisecond, second, minute, hour, or day using half-up tick arithmetic.</summary>
        /// <param name="timeUnit">The clock unit used as the rounding interval.</param>
        /// <returns>The rounded value with the original kind.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="timeUnit"/> is unsupported.</exception>
        [DebuggerStepThrough]
        public DateTime RoundToNearest(TimeUnit timeUnit)
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

            var roundedTicks = ((date.Ticks + (ticks / 2)) / ticks) * ticks;
            return new DateTime(roundedTicks, date.Kind);
        }

        /// <summary>
        /// Rounds a <see cref="DateTime"/> to the nearest arbitrary positive interval.
        /// </summary>
        /// <param name="interval">The positive interval.</param>
        /// <returns>The rounded value.</returns>
        /// <remarks><example><code>var value = timestamp.RoundToNearest(TimeSpan.FromMinutes(15));</code></example></remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is zero or negative.</exception>
        [DebuggerStepThrough]
        public DateTime RoundToNearest(TimeSpan interval)
        {
            EnsurePositiveInterval(interval);

            var roundedTicks = ((date.Ticks + (interval.Ticks / 2)) / interval.Ticks) * interval.Ticks;
            return new DateTime(roundedTicks, date.Kind);
        }

        /// <summary>
        /// Adds or subtracts working days while skipping configured holidays and non-working weekdays.
        /// </summary>
        /// <param name="days">The signed number of working days to move.</param>
        /// <param name="holidays">Dates to skip, or <see langword="null"/> for none.</param>
        /// <param name="nonWorkingDays">Weekdays to skip; Saturday and Sunday are used when omitted.</param>
        /// <returns>The value reached after the requested number of working days.</returns>
        [DebuggerStepThrough]
        public DateTime AddBusinessDays(int days, DateTime[] holidays, params DayOfWeek[] nonWorkingDays)
        {
            var configuredNonWorkingDays = nonWorkingDays is { Length: > 0 } ? nonWorkingDays : [DayOfWeek.Saturday, DayOfWeek.Sunday];
            var nonWorkingDaySet = configuredNonWorkingDays.ToHashSet();
            var holidaySet = (holidays ?? []).Select(holiday => holiday.Date).ToHashSet();

            if (days == 0)
            {
                return date;
            }

            var result = date;
            var step = days > 0 ? 1 : -1;
            var remaining = Math.Abs(days);
            while (remaining > 0)
            {
                result = result.AddDays(step);
                if (!nonWorkingDaySet.Contains(result.DayOfWeek) && !holidaySet.Contains(result.Date))
                {
                    remaining--;
                }
            }

            return result;
        }

        /// <summary>
        /// Converts a local wall-clock value in a time zone to a <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <param name="timeZone">The time zone whose rules are used.</param>
        /// <param name="invalidTimePolicy">The policy for invalid DST values.</param>
        /// <param name="ambiguousTimePolicy">The policy for ambiguous DST values.</param>
        /// <returns>A <see cref="DateTimeOffset"/> with the selected time-zone offset.</returns>
        /// <remarks>
        /// <para>The <see cref="DateTime.Kind"/> is ignored; the value is treated as a wall-clock time in <paramref name="timeZone"/>.</para>
        /// <example>
        /// <code>
        /// var instant = local.ToDateTimeOffset(timeZone, InvalidTimePolicy.MoveForward);
        /// </code>
        /// </example>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown for invalid or ambiguous times when the corresponding policy is Throw.</exception>
        [DebuggerStepThrough]
        public DateTimeOffset ToDateTimeOffset(
            TimeZoneInfo timeZone,
            InvalidTimePolicy invalidTimePolicy = InvalidTimePolicy.Throw,
            AmbiguousTimePolicy ambiguousTimePolicy = AmbiguousTimePolicy.Throw)
        {
            ArgumentNullException.ThrowIfNull(timeZone);

            var wallClock = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
            wallClock = ResolveInvalidTime(wallClock, timeZone, invalidTimePolicy);

            var offset = timeZone.IsAmbiguousTime(wallClock)
                ? ResolveAmbiguousOffset(wallClock, timeZone, ambiguousTimePolicy)
                : timeZone.GetUtcOffset(wallClock);

            return new DateTimeOffset(wallClock, offset);
        }

        /// <summary>
        /// Converts a local wall-clock value in a time zone to a UTC <see cref="DateTime"/>.
        /// </summary>
        /// <param name="timeZone">The time zone whose rules are used.</param>
        /// <param name="invalidTimePolicy">The policy for invalid DST values.</param>
        /// <param name="ambiguousTimePolicy">The policy for ambiguous DST values.</param>
        /// <returns>A UTC <see cref="DateTime"/>.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var utc = local.ToUtcDateTime(timeZone);
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public DateTime ToUtcDateTime(
            TimeZoneInfo timeZone,
            InvalidTimePolicy invalidTimePolicy = InvalidTimePolicy.Throw,
            AmbiguousTimePolicy ambiguousTimePolicy = AmbiguousTimePolicy.Throw)
        {
            return date.ToDateTimeOffset(timeZone, invalidTimePolicy, ambiguousTimePolicy).UtcDateTime;
        }

        /// <summary>
        /// Formats a <see cref="DateTime"/> as a round-trip UTC ISO string.
        /// </summary>
        /// <returns>An invariant UTC ISO timestamp.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var text = utc.ToIsoUtcString();
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public string ToIsoUtcString()
        {
            return date.ToDateTimeOffset().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats a <see cref="DateTime"/> as a file-safe UTC timestamp.
        /// </summary>
        /// <returns>An invariant timestamp such as 20260629T134530Z.</returns>
        /// <remarks>
        /// <example>
        /// <code>
        /// var fileName = $"backup-{clock.ToFileSafeTimestamp()}.zip";
        /// </code>
        /// </example>
        /// </remarks>
        [DebuggerStepThrough]
        public string ToFileSafeTimestamp()
        {
            return date.ToDateTimeOffset().UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        }
    }

    private static DateTime ResolveInvalidTime(DateTime wallClock, TimeZoneInfo timeZone, InvalidTimePolicy policy)
    {
        if (!timeZone.IsInvalidTime(wallClock))
        {
            return wallClock;
        }

        if (policy == InvalidTimePolicy.Throw)
        {
            throw new ArgumentException($"The local time {wallClock:O} is invalid in time zone {timeZone.Id}.", nameof(wallClock));
        }

        var step = policy == InvalidTimePolicy.MoveForward ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(-1);
        var result = wallClock;
        do
        {
            result = result.Add(step);
        }
        while (timeZone.IsInvalidTime(result));

        return result;
    }

    private static TimeSpan ResolveAmbiguousOffset(DateTime wallClock, TimeZoneInfo timeZone, AmbiguousTimePolicy policy)
    {
        if (policy == AmbiguousTimePolicy.Throw)
        {
            throw new ArgumentException($"The local time {wallClock:O} is ambiguous in time zone {timeZone.Id}.", nameof(wallClock));
        }

        var offsets = timeZone.GetAmbiguousTimeOffsets(wallClock).OrderBy(offset => offset).ToArray();
        return policy == AmbiguousTimePolicy.EarlierOffset ? offsets[0] : offsets[^1];
    }

    private static DateTime GetNowForKind(DateTimeKind kind)
    {
        return GetNowForKind(kind, TimeProvider.System);
    }

    private static DateTime GetNowForKind(DateTimeKind kind, TimeProvider timeProvider)
    {
        return kind switch
        {
            DateTimeKind.Utc => timeProvider.GetUtcNow().UtcDateTime,
            DateTimeKind.Local => timeProvider.GetLocalNow().DateTime,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(timeProvider.GetUtcNow().UtcDateTime, DateTimeKind.Unspecified),
            _ => timeProvider.GetLocalNow().DateTime
        };
    }

    private static void EnsurePositiveInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
        }
    }
}

/// <summary>
/// Specifies the unit of time for date-based relative operations.
/// </summary>
public enum DateUnit
{
    /// <summary>
    /// Represents a day unit for relative date calculations.
    /// </summary>
    Day,

    /// <summary>
    /// Represents a week unit (7 days) for relative date calculations.
    /// </summary>
    Week,

    /// <summary>
    /// Represents a month unit (30 days) for relative date calculations.
    /// </summary>
    Month,

    /// <summary>
    /// Represents a year unit (365 days) for relative date calculations.
    /// </summary>
    Year
}

/// <summary>
/// Specifies the unit of time for time-based relative operations.
/// </summary>
public enum TimeUnit
{
    /// <summary>
    /// Represents a millisecond unit for relative time calculations.
    /// </summary>
    Millisecond,

    /// <summary>
    /// Represents a second unit for relative time calculations.
    /// </summary>
    Second,

    /// <summary>
    /// Represents a minute unit for relative time calculations.
    /// </summary>
    Minute,

    /// <summary>
    /// Represents an hour unit for relative time calculations.
    /// </summary>
    Hour,

    /// <summary>
    /// Represents a day unit for relative time calculations.
    /// </summary>
    Day
}

/// <summary>
/// Specifies the direction for relative date/time calculations.
/// </summary>
public enum DateTimeDirection
{
    /// <summary>
    /// Indicates that the calculation should look backward in time from the reference point.
    /// </summary>
    Past,

    /// <summary>
    /// Indicates that the calculation should look forward in time from the reference point.
    /// </summary>
    Future
}

/// <summary>
/// Specifies how ambiguous slash dates such as 03/04/2026 are parsed.
/// </summary>
public enum AmbiguousDatePolicy
{
    /// <summary>
    /// Reject ambiguous slash dates.
    /// </summary>
    Reject,

    /// <summary>
    /// Interpret ambiguous slash dates as day/month/year.
    /// </summary>
    PreferDayMonthYear,

    /// <summary>
    /// Interpret ambiguous slash dates as month/day/year.
    /// </summary>
    PreferMonthDayYear
}

/// <summary>
/// Specifies how invalid local times during daylight-saving transitions are handled.
/// </summary>
public enum InvalidTimePolicy
{
    /// <summary>
    /// Throw when the local time is invalid.
    /// </summary>
    Throw,

    /// <summary>
    /// Move forward to the first valid local time.
    /// </summary>
    MoveForward,

    /// <summary>
    /// Move backward to the previous valid local time.
    /// </summary>
    MoveBackward
}

/// <summary>
/// Specifies how ambiguous local times during daylight-saving transitions are handled.
/// </summary>
public enum AmbiguousTimePolicy
{
    /// <summary>
    /// Throw when the local time is ambiguous.
    /// </summary>
    Throw,

    /// <summary>
    /// Choose the earlier offset value returned by the time-zone rule.
    /// </summary>
    EarlierOffset,

    /// <summary>
    /// Choose the later offset value returned by the time-zone rule.
    /// </summary>
    LaterOffset
}
