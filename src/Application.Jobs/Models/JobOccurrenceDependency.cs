// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents one persisted occurrence dependency link.
/// </summary>
public sealed record JobOccurrenceDependency
{
    /// <summary>
    /// Gets the dependency identifier.
    /// </summary>
    public Guid DependencyId { get; init; }

    /// <summary>
    /// Gets the dependent occurrence identifier.
    /// </summary>
    public Guid DependentOccurrenceId { get; init; }

    /// <summary>
    /// Gets the prerequisite occurrence identifier.
    /// </summary>
    public Guid PrerequisiteOccurrenceId { get; init; }

    /// <summary>
    /// Gets the required prerequisite statuses.
    /// </summary>
    public IReadOnlyList<JobOccurrenceStatus> RequiredStatuses { get; init; } = [];

    /// <summary>
    /// Gets the dependency status.
    /// </summary>
    public JobDependencyStatus Status { get; init; } = JobDependencyStatus.Pending;

    /// <summary>
    /// Gets the dependency failure policy.
    /// </summary>
    public JobDependencyFailurePolicy FailurePolicy { get; init; } = JobDependencyFailurePolicy.KeepBlocked;

    /// <summary>
    /// Gets the optional reason.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets source properties.
    /// </summary>
    public PropertyBag Properties { get; init; } = new();

    /// <summary>
    /// Gets the created audit timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets the updated audit timestamp.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }
}