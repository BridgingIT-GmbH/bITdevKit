// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides holidays from configured year-based calculations.
/// </summary>
/// <remarks><example><code>var provider = new CalculatedHolidayProvider([new CalculatedHoliday("Easter", HolidayCalculations.GregorianEasterSunday)]);</code></example></remarks>
public sealed class CalculatedHolidayProvider(IEnumerable<CalculatedHoliday> holidays) : IHolidayProvider
{
    private readonly IReadOnlyList<CalculatedHoliday> holidays = holidays?.ToArray() ?? throw new ArgumentNullException(nameof(holidays));

    /// <inheritdoc />
    public IEnumerable<Holiday> GetHolidays(int year)
    {
        foreach (var holiday in this.holidays)
        {
            ArgumentNullException.ThrowIfNull(holiday.DateFactory);

            yield return new Holiday(holiday.DateFactory(year), holiday.Name);
        }
    }
}
