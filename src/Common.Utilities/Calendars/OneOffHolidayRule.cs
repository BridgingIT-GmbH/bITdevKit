// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Marks configured one-off holidays as non-working days.
/// </summary>
/// <remarks><example><code>var rule = new OneOffHolidayRule([new Holiday(date)]);</code></example></remarks>
public sealed class OneOffHolidayRule(IEnumerable<Holiday> holidays) : IBusinessDayRule
{
    private readonly IReadOnlyDictionary<DateOnly, Holiday> holidays = holidays.ToDictionary(holiday => holiday.Date);

    /// <inheritdoc />
    public BusinessDayRuleResult Evaluate(DateOnly date)
    {
        return this.holidays.TryGetValue(date, out var holiday)
            ? new BusinessDayRuleResult(BusinessDayRuleResultKind.NonWorkingDay, FormatHolidayReason(holiday.Name))
            : BusinessDayRuleResult.NoMatch;
    }

    private static string FormatHolidayReason(string name) => string.IsNullOrWhiteSpace(name) ? "Holiday" : $"Holiday: {name}";
}
