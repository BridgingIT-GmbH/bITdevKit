// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents scheduler-owned calendar rules for recurring trigger materialization.
/// </summary>
public sealed record JobCalendarDefinition
{
    /// <summary>
    /// Gets or sets the local execution time.
    /// </summary>
    public TimeOnly TimeOfDay { get; init; } = TimeOnly.MinValue;

    /// <summary>
    /// Gets or sets a value indicating whether business-day materialization is enabled.
    /// </summary>
    public bool BusinessDaysOnly { get; init; }

    /// <summary>
    /// Gets or sets the selected weekdays.
    /// </summary>
    public IReadOnlyList<DayOfWeek> Weekdays { get; init; } = [];

    /// <summary>
    /// Gets or sets the selected days of month.
    /// Positive values are 1-based days. Negative values count back from the end of the month.
    /// </summary>
    public IReadOnlyList<int> DaysOfMonth { get; init; } = [];

    /// <summary>
    /// Gets or sets the explicit included local dates.
    /// </summary>
    public IReadOnlyList<DateOnly> ExplicitDates { get; init; } = [];

    /// <summary>
    /// Gets or sets the explicit excluded local dates.
    /// </summary>
    public IReadOnlyList<DateOnly> ExcludedDates { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the calendar has at least one selector.
    /// </summary>
    public bool HasSelectors => this.BusinessDaysOnly || this.Weekdays.Count > 0 || this.DaysOfMonth.Count > 0 || this.ExplicitDates.Count > 0;

    /// <summary>
    /// Builds a stable display expression for operational queries and documentation.
    /// </summary>
    public string ToScheduleExpression()
    {
        var parts = new List<string> { $"time={this.TimeOfDay:HH\\:mm}" };

        if (this.BusinessDaysOnly)
        {
            parts.Add("business-days");
        }

        if (this.Weekdays.Count > 0)
        {
            parts.Add($"weekdays={string.Join(',', this.Weekdays.OrderBy(x => x).Select(x => x.ToString()[..3]))}");
        }

        if (this.DaysOfMonth.Count > 0)
        {
            parts.Add($"month-days={string.Join(',', this.DaysOfMonth.OrderBy(x => x).Select(day => day == -1 ? "last" : day.ToString()))}");
        }

        if (this.ExplicitDates.Count > 0)
        {
            parts.Add($"dates={string.Join(',', this.ExplicitDates.OrderBy(x => x).Select(x => x.ToString("yyyy-MM-dd")))}");
        }

        if (this.ExcludedDates.Count > 0)
        {
            parts.Add($"exclude={string.Join(',', this.ExcludedDates.OrderBy(x => x).Select(x => x.ToString("yyyy-MM-dd")))}");
        }

        return $"calendar({string.Join(";", parts)})";
    }

    /// <inheritdoc />
    public override string ToString() => this.ToScheduleExpression();
}