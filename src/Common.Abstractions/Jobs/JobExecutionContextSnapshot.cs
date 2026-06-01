// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a previous execution snapshot exposed through <see cref="IJobExecutionContext"/>.
/// </summary>
/// <param name="OccurrenceId">The occurrence identifier.</param>
/// <param name="ExecutionId">The execution identifier.</param>
/// <param name="JobName">The stable job name.</param>
/// <param name="TriggerName">The trigger name.</param>
/// <param name="AttemptNumber">The attempt number.</param>
/// <param name="Status">The execution status.</param>
/// <param name="StartedUtc">The start time.</param>
/// <param name="CompletedUtc">The completion time.</param>
/// <param name="Messages">The recorded messages.</param>
/// <param name="ErrorMessage">The recorded error message.</param>
/// <example>
/// <code>
/// var previous = new JobExecutionContextSnapshot(
///     Guid.NewGuid(),
///     Guid.NewGuid(),
///     "sync-customers",
///     "nightly",
///     1,
///     JobExecutionStatus.Completed,
///     DateTimeOffset.UtcNow.AddMinutes(-5),
///     DateTimeOffset.UtcNow.AddMinutes(-4),
///     new[] { "Completed successfully." },
///     null);
/// </code>
/// </example>
public sealed record JobExecutionContextSnapshot(
    Guid OccurrenceId,
    Guid ExecutionId,
    string JobName,
    string TriggerName,
    int AttemptNumber,
    JobExecutionStatus Status,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc,
    IReadOnlyList<string> Messages,
    string ErrorMessage);