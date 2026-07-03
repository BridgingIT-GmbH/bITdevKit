// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines business-day operations for a calendar.
/// </summary>
/// <remarks><example><code>var due = calendar.AddBusinessDays(date, 3);</code></example></remarks>
public interface IBusinessCalendar
{
    /// <summary>Determines whether a date is a business day.</summary>
    /// <param name="date">The date to evaluate.</param>
    /// <returns><c>true</c> when the date is a business day.</returns>
    /// <remarks><example><code>var open = calendar.IsBusinessDay(date);</code></example></remarks>
    bool IsBusinessDay(DateOnly date);

    /// <summary>Gets diagnostic business-day information for a date.</summary>
    /// <param name="date">The date to evaluate.</param>
    /// <returns>Business-day information for the date.</returns>
    /// <remarks><example><code>var info = calendar.GetBusinessDayInfo(date);</code></example></remarks>
    BusinessDayInfo GetBusinessDayInfo(DateOnly date);

    /// <summary>Adds business days to a date.</summary>
    /// <param name="date">The source date.</param>
    /// <param name="days">The number of business days to add.</param>
    /// <returns>The adjusted date.</returns>
    /// <remarks><example><code>var due = calendar.AddBusinessDays(date, 2);</code></example></remarks>
    DateOnly AddBusinessDays(DateOnly date, int days);

    /// <summary>Counts business days in a half-open date range.</summary>
    /// <param name="startInclusive">The inclusive start date.</param>
    /// <param name="endExclusive">The exclusive end date.</param>
    /// <returns>The number of business days.</returns>
    /// <remarks><example><code>var count = calendar.CountBusinessDays(start, end);</code></example></remarks>
    int CountBusinessDays(DateOnly startInclusive, DateOnly endExclusive);

    /// <summary>Finds the next business day.</summary>
    /// <param name="date">The source date.</param>
    /// <param name="includeCurrent">Whether the source date may be returned.</param>
    /// <returns>The next business day.</returns>
    /// <remarks><example><code>var next = calendar.NextBusinessDay(date, includeCurrent: true);</code></example></remarks>
    DateOnly NextBusinessDay(DateOnly date, bool includeCurrent = false);

    /// <summary>Finds the previous business day.</summary>
    /// <param name="date">The source date.</param>
    /// <param name="includeCurrent">Whether the source date may be returned.</param>
    /// <returns>The previous business day.</returns>
    /// <remarks><example><code>var previous = calendar.PreviousBusinessDay(date);</code></example></remarks>
    DateOnly PreviousBusinessDay(DateOnly date, bool includeCurrent = false);
}
