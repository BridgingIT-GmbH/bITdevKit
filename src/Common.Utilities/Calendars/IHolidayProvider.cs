// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides holidays for a calendar year.
/// </summary>
/// <remarks><example><code>var holidays = provider.GetHolidays(2026);</code></example></remarks>
public interface IHolidayProvider
{
    /// <summary>
    /// Gets the holidays for a year.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <returns>The holidays for the year.</returns>
    /// <remarks><example><code>var holidays = provider.GetHolidays(2026);</code></example></remarks>
    IEnumerable<Holiday> GetHolidays(int year);
}
