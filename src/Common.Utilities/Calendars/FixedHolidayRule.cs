// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Marks configured fixed annual holidays as non-working days.
/// </summary>
/// <remarks><example><code>var rule = new FixedHolidayRule([new FixedHoliday(12, 25, "Christmas")]);</code></example></remarks>
public sealed class FixedHolidayRule(IEnumerable<FixedHoliday> holidays) : IBusinessDayRule
{
    private readonly IReadOnlyList<FixedHoliday> holidays = holidays.ToArray();

    /// <inheritdoc />
    public BusinessDayRuleResult Evaluate(DateOnly date)
    {
        var holiday = this.holidays.FirstOrDefault(item => item.Month == date.Month && item.Day == date.Day);
        return holiday == default
            ? BusinessDayRuleResult.NoMatch
            : new BusinessDayRuleResult(BusinessDayRuleResultKind.NonWorkingDay, string.IsNullOrWhiteSpace(holiday.Name) ? "Holiday" : $"Holiday: {holiday.Name}");
    }
}
