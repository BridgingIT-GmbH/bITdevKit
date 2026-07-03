// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Provides business-calendar extension methods for date values.
/// </summary>
/// <remarks><example><code>var due = start.AddBusinessDays(3, CultureInfo.GetCultureInfo("nl-NL"));</code></example></remarks>
public static class BusinessDateExtensions
{
    /// <summary>
    /// Adds business days using an explicit business calendar.
    /// </summary>
    /// <param name="source">The source date.</param>
    /// <param name="days">The number of business days to add. Negative values move backward.</param>
    /// <param name="calendar">The calendar used to evaluate business days.</param>
    /// <returns>The adjusted business date.</returns>
    /// <remarks><example><code>var due = start.AddBusinessDays(3, calendar);</code></example></remarks>
    [DebuggerStepThrough]
    public static DateOnly AddBusinessDays(this DateOnly source, int days, IBusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return calendar.AddBusinessDays(source, days);
    }

    /// <summary>
    /// Adds business days using the globally registered calendar for the current culture.
    /// </summary>
    /// <param name="source">The source date.</param>
    /// <param name="days">The number of business days to add. Negative values move backward.</param>
    /// <returns>The adjusted business date.</returns>
    /// <remarks><example><code>var due = start.AddBusinessDays(3);</code></example></remarks>
    [DebuggerStepThrough]
    public static DateOnly AddBusinessDays(this DateOnly source, int days)
    {
        return source.AddBusinessDays(days, BusinessCalendars.Resolve());
    }

    /// <summary>
    /// Adds business days using the globally registered calendar for a culture.
    /// </summary>
    /// <param name="source">The source date.</param>
    /// <param name="days">The number of business days to add. Negative values move backward.</param>
    /// <param name="culture">The culture used to resolve the calendar.</param>
    /// <returns>The adjusted business date.</returns>
    /// <remarks><example><code>var due = start.AddBusinessDays(3, CultureInfo.GetCultureInfo("nl-NL"));</code></example></remarks>
    [DebuggerStepThrough]
    public static DateOnly AddBusinessDays(this DateOnly source, int days, CultureInfo culture)
    {
        return source.AddBusinessDays(days, BusinessCalendars.Resolve(culture));
    }

    /// <summary>
    /// Determines whether a date is a business day in the supplied calendar.
    /// </summary>
    /// <param name="source">The date to evaluate.</param>
    /// <param name="calendar">The calendar used to evaluate business days.</param>
    /// <returns><c>true</c> when the date is a business day; otherwise <c>false</c>.</returns>
    /// <remarks><example><code>var open = date.IsBusinessDay(calendar);</code></example></remarks>
    [DebuggerStepThrough]
    public static bool IsBusinessDay(this DateOnly source, IBusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return calendar.IsBusinessDay(source);
    }

    /// <summary>
    /// Determines whether a date is a business day using the globally registered calendar for the current culture.
    /// </summary>
    /// <param name="source">The date to evaluate.</param>
    /// <returns><c>true</c> when the date is a business day; otherwise <c>false</c>.</returns>
    /// <remarks><example><code>var open = date.IsBusinessDay();</code></example></remarks>
    [DebuggerStepThrough]
    public static bool IsBusinessDay(this DateOnly source)
    {
        return source.IsBusinessDay(BusinessCalendars.Resolve());
    }

    /// <summary>
    /// Determines whether a date is a business day using the globally registered calendar for a culture.
    /// </summary>
    /// <param name="source">The date to evaluate.</param>
    /// <param name="culture">The culture used to resolve the calendar.</param>
    /// <returns><c>true</c> when the date is a business day; otherwise <c>false</c>.</returns>
    /// <remarks><example><code>var open = date.IsBusinessDay(CultureInfo.GetCultureInfo("de-DE"));</code></example></remarks>
    [DebuggerStepThrough]
    public static bool IsBusinessDay(this DateOnly source, CultureInfo culture)
    {
        return source.IsBusinessDay(BusinessCalendars.Resolve(culture));
    }

    /// <summary>
    /// Adds business days with an explicit business calendar while preserving time and kind.
    /// </summary>
    /// <param name="source">The source date and time.</param>
    /// <param name="days">The number of business days to add. Negative values move backward.</param>
    /// <param name="calendar">The calendar used to evaluate business days.</param>
    /// <returns>The adjusted date and time.</returns>
    /// <remarks><example><code>var due = createdAt.AddBusinessDays(2, calendar);</code></example></remarks>
    [DebuggerStepThrough]
    public static DateTime AddBusinessDays(this DateTime source, int days, IBusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var date = DateOnly.FromDateTime(source);
        var result = calendar.AddBusinessDays(date, days);
        return new DateTime(result.Year, result.Month, result.Day, source.Hour, source.Minute, source.Second, source.Millisecond, source.Kind)
            .AddTicks(source.Ticks % TimeSpan.TicksPerMillisecond);
    }

    /// <summary>
    /// Adds business days using the globally registered calendar for the current culture.
    /// </summary>
    /// <param name="source">The source date and time.</param>
    /// <param name="days">The number of business days to add. Negative values move backward.</param>
    /// <returns>The adjusted date and time.</returns>
    /// <remarks><example><code>var due = createdAt.AddBusinessDays(2);</code></example></remarks>
    [DebuggerStepThrough]
    public static DateTime AddBusinessDays(this DateTime source, int days)
    {
        return source.AddBusinessDays(days, BusinessCalendars.Resolve());
    }

    /// <summary>
    /// Adds business days using the globally registered calendar for a culture.
    /// </summary>
    /// <param name="source">The source date and time.</param>
    /// <param name="days">The number of business days to add. Negative values move backward.</param>
    /// <param name="culture">The culture used to resolve the calendar.</param>
    /// <returns>The adjusted date and time.</returns>
    /// <remarks><example><code>var due = createdAt.AddBusinessDays(2, CultureInfo.GetCultureInfo("nl-NL"));</code></example></remarks>
    [DebuggerStepThrough]
    public static DateTime AddBusinessDays(this DateTime source, int days, CultureInfo culture)
    {
        return source.AddBusinessDays(days, BusinessCalendars.Resolve(culture));
    }
}
