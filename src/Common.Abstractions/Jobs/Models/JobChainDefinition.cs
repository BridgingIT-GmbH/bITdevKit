// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents a code-first chained successor template.
/// </summary>
public sealed record JobChainDefinition
{
    /// <summary>
    /// Gets or sets the successor job name.
    /// </summary>
    public string SuccessorJobName { get; init; }

    /// <summary>
    /// Gets or sets the optional successor trigger name.
    /// </summary>
    public string SuccessorTriggerName { get; init; }

    /// <summary>
    /// Gets or sets the required prerequisite statuses.
    /// </summary>
    public IReadOnlyList<JobOccurrenceStatus> RequiredStatuses { get; init; } = [JobOccurrenceStatus.Completed];

    /// <summary>
    /// Gets or sets the dependency failure policy.
    /// </summary>
    public JobDependencyFailurePolicy FailurePolicy { get; init; } = JobDependencyFailurePolicy.KeepBlocked;

    /// <summary>
    /// Gets or sets optional chain properties.
    /// </summary>
    public PropertyBag Properties { get; init; } = new();
}