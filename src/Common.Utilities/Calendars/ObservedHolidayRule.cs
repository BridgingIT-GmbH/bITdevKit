// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Applies an observed holiday rule for fixed holidays that fall on a weekend.
/// </summary>
/// <remarks><example><code>var rule = new ObservedHolidayRule([new FixedHoliday(1, 1, "New Year")]);</code></example></remarks>
public sealed class ObservedHolidayRule(IEnumerable<FixedHoliday> holidays) : IBusinessDayRule
{
    private readonly IReadOnlyList<FixedHoliday> holidays = holidays.ToArray();

    /// <inheritdoc />
    public BusinessDayRuleResult Evaluate(DateOnly date)
    {
        foreach (var holiday in this.holidays)
        {
            var actual = new DateOnly(date.Year, holiday.Month, holiday.Day);
            var observed = actual.DayOfWeek switch
            {
                DayOfWeek.Saturday => actual.AddDays(-1),
                DayOfWeek.Sunday => actual.AddDays(1),
                _ => actual
            };

            if (date == observed && observed != actual)
            {
                return new BusinessDayRuleResult(BusinessDayRuleResultKind.NonWorkingDay, string.IsNullOrWhiteSpace(holiday.Name) ? "Observed holiday" : $"Observed holiday: {holiday.Name}");
            }
        }

        return BusinessDayRuleResult.NoMatch;
    }
}
