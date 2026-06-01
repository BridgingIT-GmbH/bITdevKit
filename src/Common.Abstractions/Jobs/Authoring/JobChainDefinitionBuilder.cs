// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Builds a chained successor occurrence template.
/// </summary>
public class JobChainDefinitionBuilder
{
    private readonly Dictionary<string, string> properties = [];
    private readonly string predecessorJobName;
    private string successorTriggerName;
    private JobDependencyFailurePolicy failurePolicy = JobDependencyFailurePolicy.KeepBlocked;
    private IReadOnlyList<JobOccurrenceStatus> requiredStatuses = [JobOccurrenceStatus.Completed];

    /// <summary>
    /// Initializes a new instance of the <see cref="JobChainDefinitionBuilder"/> class.
    /// </summary>
    public JobChainDefinitionBuilder(string predecessorJobName, string successorJobName)
    {
        this.predecessorJobName = predecessorJobName;
        this.SuccessorJobName = string.IsNullOrWhiteSpace(successorJobName)
            ? throw new InvalidOperationException($"The job '{predecessorJobName}' requires a non-empty successor job name.")
            : successorJobName.Trim();
    }

    /// <summary>
    /// Gets the successor job name.
    /// </summary>
    public string SuccessorJobName { get; }

    /// <summary>
    /// Sets the successor trigger name.
    /// </summary>
    public JobChainDefinitionBuilder WithTrigger(string value)
    {
        this.successorTriggerName = string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"The chained successor '{this.SuccessorJobName}' requires a non-empty trigger name when configured explicitly.")
            : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the required prerequisite statuses.
    /// </summary>
    public JobChainDefinitionBuilder RequireStatuses(params JobOccurrenceStatus[] values)
    {
        this.requiredStatuses = values?.Length > 0
            ? values.Distinct().ToArray()
            : throw new InvalidOperationException($"The chained successor '{this.SuccessorJobName}' requires at least one prerequisite status.");
        return this;
    }

    /// <summary>
    /// Sets the dependency failure policy.
    /// </summary>
    public JobChainDefinitionBuilder WithFailurePolicy(JobDependencyFailurePolicy value)
    {
        this.failurePolicy = value;
        return this;
    }

    /// <summary>
    /// Sets chain properties.
    /// </summary>
    public JobChainDefinitionBuilder WithProperty(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException($"The chained successor '{this.SuccessorJobName}' requires non-empty property keys.");
        }

        this.properties[key.Trim()] = value ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Builds the immutable chain definition.
    /// </summary>
    public JobChainDefinition Build()
    {
        return new JobChainDefinition
        {
            SuccessorJobName = this.SuccessorJobName,
            SuccessorTriggerName = this.successorTriggerName,
            RequiredStatuses = this.requiredStatuses,
            FailurePolicy = this.failurePolicy,
            Properties = new PropertyBag(this.properties.ToDictionary(x => x.Key, x => (object)x.Value, StringComparer.OrdinalIgnoreCase))
            {
                ["chain:predecessorJob"] = this.predecessorJobName,
            },
        };
    }
}