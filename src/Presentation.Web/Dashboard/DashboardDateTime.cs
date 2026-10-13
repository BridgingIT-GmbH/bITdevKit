// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Globalization;

/// <summary>Formats and parses dashboard date/time filters independently of the server's local time zone.</summary>
/// <example><code>var instant = DashboardDateTime.ParseUtc("2026-10-12T09:00:00.123Z");</code></example>
public static class DashboardDateTime
{
    /// <summary>Parses a filter timestamp as UTC, retaining supplied offsets and time precision.</summary>
    /// <param name="value">An ISO timestamp or a timezone-free UTC date/time input value.</param>
    /// <returns>The equivalent UTC instant, or null for an empty or invalid value.</returns>
    /// <example><code>var instant = DashboardDateTime.ParseUtc("2026-10-12T09:30");</code></example>
    public static DateTimeOffset? ParseUtc(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>Formats a UTC instant for a minute-precision datetime-local input.</summary>
    /// <param name="value">The instant to display.</param>
    /// <returns>A timezone-free UTC input value, or an empty string.</returns>
    /// <example><code>var input = DashboardDateTime.FormatInput(DateTimeOffset.UtcNow);</code></example>
    public static string FormatInput(DateTimeOffset? value)
    {
        return value?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>Formats an explicit UTC query timestamp without losing sub-millisecond precision.</summary>
    /// <param name="value">The instant to serialize.</param>
    /// <returns>An ISO timestamp ending in Z, or null when no bound is specified.</returns>
    /// <example><code>var queryValue = DashboardDateTime.FormatQuery(DateTimeOffset.UtcNow);</code></example>
    public static string FormatQuery(DateTimeOffset? value)
    {
        return value?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }
}
