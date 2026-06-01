// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides cron parsing, validation and occurrence calculation for the Jobs feature.
/// </summary>
/// <example>
/// <code>
/// var engine = new CronosJobCronEngine();
/// var next = engine.GetNextOccurrenceUtc("*/5 * * * *", DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
/// </code>
/// </example>
public interface IJobCronEngine
{
    /// <summary>
    /// Validates a cron expression.
    /// </summary>
    /// <param name="expression">The cron expression.</param>
    /// <returns>A success or failure result.</returns>
    Result Validate(string expression);

    /// <summary>
    /// Gets the next occurrence in UTC after the supplied instant.
    /// </summary>
    /// <param name="expression">The cron expression.</param>
    /// <param name="fromUtc">The lower bound in UTC.</param>
    /// <param name="timeZone">The time zone used for cron calculation.</param>
    /// <returns>The next occurrence in UTC when one exists.</returns>
    Result<DateTimeOffset?> GetNextOccurrenceUtc(
        string expression,
        DateTimeOffset fromUtc,
        TimeZoneInfo timeZone);

    /// <summary>
    /// Gets cron occurrences in UTC within the supplied range.
    /// </summary>
    /// <param name="expression">The cron expression.</param>
    /// <param name="fromUtc">The lower bound in UTC.</param>
    /// <param name="toUtc">The upper bound in UTC.</param>
    /// <param name="timeZone">The time zone used for cron calculation.</param>
    /// <param name="fromInclusive">Indicates whether the lower bound is inclusive.</param>
    /// <param name="toInclusive">Indicates whether the upper bound is inclusive.</param>
    /// <returns>The matching occurrences in UTC.</returns>
    Result<IReadOnlyList<DateTimeOffset>> GetOccurrencesUtc(
        string expression,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        TimeZoneInfo timeZone,
        bool fromInclusive = false,
        bool toInclusive = true);
}