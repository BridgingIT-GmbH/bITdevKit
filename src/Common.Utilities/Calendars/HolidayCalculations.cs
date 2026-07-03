// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

/// <summary>
/// Provides holiday date calculations.
/// </summary>
/// <remarks><example><code>var easter = HolidayCalculations.GregorianEasterSunday(2026);</code></example></remarks>
public static class HolidayCalculations
{
    /// <summary>
    /// Calculates Gregorian Easter Sunday using the Meeus/Jones/Butcher algorithm.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <returns>Easter Sunday for the year.</returns>
    /// <remarks><example><code>var goodFriday = HolidayCalculations.GregorianEasterSunday(2026).AddDays(-2);</code></example></remarks>
    [DebuggerStepThrough]
    public static DateOnly GregorianEasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var month = (h + l - (7 * m) + 114) / 31;
        var day = ((h + l - (7 * m) + 114) % 31) + 1;

        return new DateOnly(year, month, day);
    }
}
