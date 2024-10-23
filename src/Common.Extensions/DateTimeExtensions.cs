// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

public static class DateTimeExtensions
{
    [DebuggerStepThrough]
    public static DateTime StartOfDay(this DateTime source)
    {
        return new DateTime(source.Year, source.Month, source.Day, 0, 0, 0, 0, source.Kind);
    }

    [DebuggerStepThrough]
    public static DateTime EndOfDay(this DateTime source)
    {
        return source.StartOfDay().AddDays(1).AddSeconds(-1);
    }

    [DebuggerStepThrough]
    public static DateTime StartOfWeek(this DateTime source)
    {
        return StartOfWeek(source, CultureInfo.InvariantCulture.DateTimeFormat.FirstDayOfWeek);
    }

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
    public static DateTime StartOfMonth(this DateTime source)
    {
        return new DateTime(source.Year, source.Month, 1, 0, 0, 0, 0, source.Kind);
    }

    [DebuggerStepThrough]
    public static DateTime EndOfMonth(this DateTime source)
    {
        return source.StartOfMonth().AddMonths(1).AddSeconds(-1);
    }

    [DebuggerStepThrough]
    public static DateTime StartOfYear(this DateTime source)
    {
        return new DateTime(source.Year, 1, 1, 0, 0, 0, 0, source.Kind);
    }

    [DebuggerStepThrough]
    public static DateTime EndOfYear(this DateTime source)
    {
        return source.StartOfYear().AddYears(1).AddSeconds(-1);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset StartOfDay(this DateTimeOffset source)
    {
        return new DateTimeOffset(source.Year, source.Month, source.Day, 0, 0, 0, 0, source.Offset);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset EndOfDay(this DateTimeOffset source)
    {
        return source.StartOfDay().AddDays(1).AddSeconds(-1);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset StartOfWeek(this DateTimeOffset source)
    {
        return StartOfWeek(source, CultureInfo.InvariantCulture.DateTimeFormat.FirstDayOfWeek);
    }

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
    public static DateTimeOffset StartOfMonth(this DateTimeOffset source)
    {
        return new DateTimeOffset(source.Year, source.Month, 1, 0, 0, 0, 0, source.Offset);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset EndOfMonth(this DateTimeOffset source)
    {
        return source.StartOfMonth().AddMonths(1).AddSeconds(-1);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset StartOfYear(this DateTimeOffset source)
    {
        return new DateTimeOffset(source.Year, 1, 1, 0, 0, 0, 0, source.Offset);
    }

    [DebuggerStepThrough]
    public static DateTimeOffset EndOfYear(this DateTimeOffset source)
    {
        return source.StartOfYear().AddYears(1).AddSeconds(-1);
    }

    [DebuggerStepThrough]
    public static DateTime? ParseDateOrEpoch(this string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return null;
        }

        // Try parsing as epoch (Unix timestamp)
        if (long.TryParse(source, out var epoch) && epoch is > 99999999 or < 0) // don't clash with compact format
        {
            return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
        }

        var formats = new[]
        {
            "yyyy-MM-dd", // ISO 8601 (e.g., "2024-03-14")
            "yyyy-MM-ddTHH:mm:ss", // ISO 8601 with time (e.g., "2024-03-14T13:45:30")
            "yyyy-MM-ddTHH:mm:ssZ", // ISO 8601 with UTC (e.g., "2024-03-14T13:45:30Z")
            "yyyy-MM-ddTHH:mm:ss.fffffff", // ISO 8601 with milliseconds (e.g., "2024-03-14T13:45:30.1234567")
            "dd/MM/yyyy", // UK format (e.g., "14/03/2024")
            "MM/dd/yyyy", // US format (e.g., "03/14/2024")
            "dd-MM-yyyy", // Alternative format (e.g., "14-03-2024")
            "dd.MM.yyyy", // European format (e.g., "14.03.2024")
            "yyyyMMdd", // Compact format (e.g., "20240314")
            "dd MMM yyyy", // Month name format (e.g., "14 Mar 2024")
            "d MMMM yyyy" // Full month name format (e.g., "14 March 2024")
        };

        // Try parsing as ISO 8601 date string
        if (DateTime.TryParseExact(
                source,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        throw new ArgumentException($"Invalid date format: {source}. Useany format or Unix epoch.");
    }

    public static bool TryParseDateOrEpoch(this string source, out DateTime result)
    {
        result = DateTime.MinValue;

        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        // Try parsing as epoch (Unix timestamp)
        if (long.TryParse(source, out var epoch) && epoch is > 99999999 or < 0) // don't clash with compact format
        {
            try
            {
                result = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Define accepted formats
        var formats = new[]
        {
            "yyyy-MM-dd", // ISO 8601 (e.g., "2024-03-14")
            "yyyy-MM-ddTHH:mm:ss", // ISO 8601 with time (e.g., "2024-03-14T13:45:30")
            "yyyy-MM-ddTHH:mm:ssZ", // ISO 8601 with UTC (e.g., "2024-03-14T13:45:30Z")
            "yyyy-MM-ddTHH:mm:ss.fffffff", // ISO 8601 with milliseconds (e.g., "2024-03-14T13:45:30.1234567")
            "dd/MM/yyyy", // UK format (e.g., "14/03/2024")
            "MM/dd/yyyy", // US format (e.g., "03/14/2024")
            "dd-MM-yyyy", // Alternative format (e.g., "14-03-2024")
            "dd.MM.yyyy", // European format (e.g., "14.03.2024")
            "yyyyMMdd", // Compact format (e.g., "20240314")
            "dd MMM yyyy", // Month name format (e.g., "14 Mar 2024")
            "d MMMM yyyy" // Full month name format (e.g., "14 March 2024")
        };

        // Try parsing with exact formats first
        if (DateTime.TryParseExact(
                source,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exactDate))
        {
            result = exactDate;

            return true;
        }

        // Try general parsing as fallback
        return DateTime.TryParse(
            source,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }
}