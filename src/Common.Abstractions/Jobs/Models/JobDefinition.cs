// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Represents a resolved code-first job definition.
/// </summary>
/// <example>
/// <code>
/// var definition = new JobDefinition
/// {
///     JobName = "cleanup",
///     DisplayName = "cleanup-job",
///     Description = "Removes stale records.",
/// };
/// </code>
/// </example>
public sealed record JobDefinition
{
    /// <summary>
    /// The default group name.
    /// </summary>
    public const string DefaultGroup = "DEFAULT";

    /// <summary>
    /// Gets or sets the stable job name.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Gets or sets the resolved display name.
    /// </summary>
    public string DisplayName { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the display name was explicitly configured.
    /// </summary>
    public bool HasExplicitDisplayName { get; init; }

    /// <summary>
    /// Gets or sets the optional description.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets or sets the job type.
    /// </summary>
    public Type JobType { get; init; }

    /// <summary>
    /// Gets or sets the resolved group.
    /// </summary>
    public string Group { get; init; } = DefaultGroup;

    /// <summary>
    /// Gets or sets the optional module.
    /// </summary>
    public string Module { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the job is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets the job service lifetime.
    /// </summary>
    public ServiceLifetime Lifetime { get; init; } = ServiceLifetime.Transient;

    /// <summary>
    /// Gets or sets the default priority.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets or sets the default timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets or sets the default retry policy.
    /// </summary>
    public JobRetryPolicy RetryPolicy { get; init; }

    /// <summary>
    /// Gets or sets the concurrency settings.
    /// </summary>
    public JobConcurrencyOptions Concurrency { get; init; } = JobConcurrencyOptions.Default;

    /// <summary>
    /// Gets or sets the resolved data type.
    /// </summary>
    public Type DataType { get; init; }

    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    public PropertyBag Properties { get; init; } = new();

    /// <summary>
    /// Gets or sets the eligible scheduler instance targets.
    /// </summary>
    public IReadOnlyList<string> TargetInstances { get; init; } = [];

    /// <summary>
    /// Gets or sets the trigger definitions.
    /// </summary>
    public IReadOnlyList<JobTriggerDefinition> Triggers { get; init; } = [];

    /// <summary>
    /// Gets or sets the chained successor definitions.
    /// </summary>
    public IReadOnlyList<JobChainDefinition> Chains { get; init; } = [];

    /// <summary>
    /// Gets or sets the job-specific behavior types.
    /// </summary>
    public IReadOnlyList<Type> BehaviorTypes { get; init; } = [];
}
