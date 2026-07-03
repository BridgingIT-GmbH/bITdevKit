// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Default business calendar with configurable non-working weekdays, holidays, and rules.
/// </summary>
/// <remarks><example><code>var calendar = new BusinessCalendar(holidays: [new DateOnly(2026, 1, 1)]);</code></example></remarks>
public sealed class BusinessCalendar : IBusinessCalendar
{
    private readonly HashSet<DayOfWeek> nonWorkingDays;
    private readonly HashSet<DateOnly> holidays;
    private readonly IReadOnlyList<IBusinessDayRule> rules;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessCalendar"/> class.
    /// </summary>
    /// <param name="nonWorkingDays">The non-working weekdays. Saturday and Sunday are used when omitted.</param>
    /// <param name="holidays">One-off holiday dates.</param>
    /// <param name="rules">Explicit rules evaluated before holidays and weekdays.</param>
    public BusinessCalendar(
        IEnumerable<DayOfWeek> nonWorkingDays = null,
        IEnumerable<DateOnly> holidays = null,
        IEnumerable<IBusinessDayRule> rules = null)
    {
        var configuredNonWorkingDays = nonWorkingDays?.ToArray();
        this.nonWorkingDays = (configuredNonWorkingDays is { Length: > 0 } ? configuredNonWorkingDays : [DayOfWeek.Saturday, DayOfWeek.Sunday]).ToHashSet();
        this.holidays = (holidays ?? []).ToHashSet();
        this.rules = (rules ?? []).ToArray();
    }

    /// <inheritdoc />
    public bool IsBusinessDay(DateOnly date) => this.GetBusinessDayInfo(date).IsBusinessDay;

    /// <inheritdoc />
    public BusinessDayInfo GetBusinessDayInfo(DateOnly date)
    {
        foreach (var rule in this.rules)
        {
            var result = rule.Evaluate(date);
            if (result.Kind == BusinessDayRuleResultKind.WorkingDay)
            {
                return new BusinessDayInfo(date, true, result.Reason);
            }

            if (result.Kind == BusinessDayRuleResultKind.NonWorkingDay)
            {
                return new BusinessDayInfo(date, false, result.Reason);
            }
        }

        if (this.holidays.Contains(date))
        {
            return new BusinessDayInfo(date, false, "Holiday");
        }

        if (this.nonWorkingDays.Contains(date.DayOfWeek))
        {
            return new BusinessDayInfo(date, false, "Weekend");
        }

        return new BusinessDayInfo(date, true);
    }

    /// <inheritdoc />
    public DateOnly AddBusinessDays(DateOnly date, int days)
    {
        if (days == 0)
        {
            return date;
        }

        var result = date;
        var step = days > 0 ? 1 : -1;
        var remaining = Math.Abs(days);
        while (remaining > 0)
        {
            result = result.AddDays(step);
            if (this.IsBusinessDay(result))
            {
                remaining--;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public int CountBusinessDays(DateOnly startInclusive, DateOnly endExclusive)
    {
        if (endExclusive < startInclusive)
        {
            throw new ArgumentException("End date must be greater than or equal to start date.", nameof(endExclusive));
        }

        var count = 0;
        for (var date = startInclusive; date < endExclusive; date = date.AddDays(1))
        {
            if (this.IsBusinessDay(date))
            {
                count++;
            }
        }

        return count;
    }

    /// <inheritdoc />
    public DateOnly NextBusinessDay(DateOnly date, bool includeCurrent = false)
    {
        var result = includeCurrent ? date : date.AddDays(1);
        while (!this.IsBusinessDay(result))
        {
            result = result.AddDays(1);
        }

        return result;
    }

    /// <inheritdoc />
    public DateOnly PreviousBusinessDay(DateOnly date, bool includeCurrent = false)
    {
        var result = includeCurrent ? date : date.AddDays(-1);
        while (!this.IsBusinessDay(result))
        {
            result = result.AddDays(-1);
        }

        return result;
    }
}
