// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;

/// <summary>
/// Business calendar whose holidays are resolved dynamically per year.
/// </summary>
/// <remarks><example><code>var calendar = new DynamicBusinessCalendar(new CalculatedHolidayProvider([new CalculatedHoliday("Easter", HolidayCalculations.GregorianEasterSunday)]));</code></example></remarks>
public sealed class DynamicBusinessCalendar : IBusinessCalendar
{
    private readonly HashSet<DayOfWeek> nonWorkingDays;
    private readonly IHolidayProvider holidayProvider;
    private readonly IReadOnlyList<IBusinessDayRule> rules;
    private readonly ConcurrentDictionary<int, IReadOnlyDictionary<DateOnly, Holiday>> holidaysByYear = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicBusinessCalendar"/> class.
    /// </summary>
    /// <param name="holidayProvider">The dynamic holiday provider.</param>
    /// <param name="nonWorkingDays">The non-working weekdays. Saturday and Sunday are used when omitted.</param>
    /// <param name="rules">Explicit rules evaluated before holidays and weekdays.</param>
    public DynamicBusinessCalendar(
        IHolidayProvider holidayProvider,
        IEnumerable<DayOfWeek> nonWorkingDays = null,
        IEnumerable<IBusinessDayRule> rules = null)
    {
        ArgumentNullException.ThrowIfNull(holidayProvider);

        var configuredNonWorkingDays = nonWorkingDays?.ToArray();
        this.nonWorkingDays = (configuredNonWorkingDays is { Length: > 0 } ? configuredNonWorkingDays : [DayOfWeek.Saturday, DayOfWeek.Sunday]).ToHashSet();
        this.holidayProvider = holidayProvider;
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

        var holidays = this.holidaysByYear.GetOrAdd(date.Year, this.GetHolidaysForYear);
        if (holidays.TryGetValue(date, out var holiday))
        {
            return new BusinessDayInfo(date, false, string.IsNullOrWhiteSpace(holiday.Name) ? "Holiday" : $"Holiday: {holiday.Name}");
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

    private IReadOnlyDictionary<DateOnly, Holiday> GetHolidaysForYear(int year)
    {
        return this.holidayProvider.GetHolidays(year).ToDictionary(holiday => holiday.Date);
    }
}
