// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a holiday calculated from a year.
/// </summary>
/// <param name="Name">The holiday name.</param>
/// <param name="DateFactory">The date calculation.</param>
/// <remarks><example><code>var goodFriday = new CalculatedHoliday("Good Friday", year =&gt; HolidayCalculations.GregorianEasterSunday(year).AddDays(-2));</code></example></remarks>
public sealed record CalculatedHoliday(string Name, Func<int, DateOnly> DateFactory);
