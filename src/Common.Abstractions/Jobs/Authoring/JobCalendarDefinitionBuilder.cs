// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Builds scheduler-owned calendar rules for calendar triggers.
/// </summary>
public class JobCalendarDefinitionBuilder
{
    private readonly HashSet<DayOfWeek> weekdays = [];
    private readonly HashSet<int> daysOfMonth = [];
    private readonly HashSet<DateOnly> explicitDates = [];
    private readonly HashSet<DateOnly> excludedDates = [];
    private TimeOnly timeOfDay = TimeOnly.MinValue;
    private bool businessDaysOnly;

    /// <summary>
    /// Sets the local execution time.
    /// </summary>
    public JobCalendarDefinitionBuilder At(TimeOnly value)
    {
        this.timeOfDay = value;
        return this;
    }

    /// <summary>
    /// Includes Monday through Friday.
    /// </summary>
    public JobCalendarDefinitionBuilder OnBusinessDays()
    {
        this.businessDaysOnly = true;
        return this;
    }

    /// <summary>
    /// Includes the supplied weekdays.
    /// </summary>
    public JobCalendarDefinitionBuilder OnWeekdays(params DayOfWeek[] values)
    {
        foreach (var value in values ?? [])
        {
            this.weekdays.Add(value);
        }

        return this;
    }

    /// <summary>
    /// Includes the supplied days of the month.
    /// Positive values are 1-based days. Negative values count back from the end of the month.
    /// </summary>
    public JobCalendarDefinitionBuilder OnDaysOfMonth(params int[] values)
    {
        foreach (var value in values ?? [])
        {
            if (value == 0 || value < -31 || value > 31)
            {
                throw new InvalidOperationException("Calendar day-of-month values must be between 1 and 31, or between -1 and -31 for end-of-month offsets.");
            }

            this.daysOfMonth.Add(value);
        }

        return this;
    }

    /// <summary>
    /// Includes the last day of every month.
    /// </summary>
    public JobCalendarDefinitionBuilder LastDayOfMonth()
    {
        this.daysOfMonth.Add(-1);
        return this;
    }

    /// <summary>
    /// Includes the supplied explicit local dates.
    /// </summary>
    public JobCalendarDefinitionBuilder OnExplicitDates(params DateOnly[] values)
    {
        foreach (var value in values ?? [])
        {
            this.explicitDates.Add(value);
        }

        return this;
    }

    /// <summary>
    /// Excludes the supplied local dates after all inclusions are evaluated.
    /// </summary>
    public JobCalendarDefinitionBuilder ExcludeDates(params DateOnly[] values)
    {
        foreach (var value in values ?? [])
        {
            this.excludedDates.Add(value);
        }

        return this;
    }

    /// <summary>
    /// Builds the immutable calendar definition.
    /// </summary>
    public JobCalendarDefinition Build()
    {
        var definition = new JobCalendarDefinition
        {
            TimeOfDay = this.timeOfDay,
            BusinessDaysOnly = this.businessDaysOnly,
            Weekdays = this.weekdays.OrderBy(x => x).ToArray(),
            DaysOfMonth = this.daysOfMonth.OrderBy(x => x).ToArray(),
            ExplicitDates = this.explicitDates.OrderBy(x => x).ToArray(),
            ExcludedDates = this.excludedDates.OrderBy(x => x).ToArray(),
        };

        if (!definition.HasSelectors)
        {
            throw new InvalidOperationException("Calendar triggers require at least one business-day, weekday, day-of-month, or explicit-date selector.");
        }

        return definition;
    }
}