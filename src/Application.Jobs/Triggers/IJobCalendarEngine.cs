// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Calculates occurrences for scheduler-owned calendar trigger rules.
/// </summary>
/// <example>
/// <code>
/// var engine = new DefaultJobCalendarEngine();
/// var definition = new JobCalendarDefinitionBuilder()
///     .At(new TimeOnly(9, 0))
///     .OnBusinessDays()
///     .Build();
/// </code>
/// </example>
public interface IJobCalendarEngine
{
    /// <summary>
    /// Validates the supplied calendar definition.
    /// </summary>
    Result Validate(JobCalendarDefinition definition);

    /// <summary>
    /// Gets the next occurrence after the supplied UTC instant.
    /// </summary>
    Result<DateTimeOffset?> GetNextOccurrenceUtc(
        JobCalendarDefinition definition,
        DateTimeOffset fromUtc,
        TimeZoneInfo timeZone);

    /// <summary>
    /// Gets the occurrences between the supplied UTC bounds.
    /// </summary>
    Result<IReadOnlyList<DateTimeOffset>> GetOccurrencesUtc(
        JobCalendarDefinition definition,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        TimeZoneInfo timeZone,
        bool fromInclusive = false,
        bool toInclusive = true);
}