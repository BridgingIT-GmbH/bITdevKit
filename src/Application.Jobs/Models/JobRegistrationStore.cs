// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Stores code-registered job definitions and applies appsettings overrides.
/// </summary>
/// <example>
/// <code>
/// var store = new JobRegistrationStore();
/// store.Add(definition);
/// var resolved = store.GetDefinitions();
/// </code>
/// </example>
public class JobRegistrationStore : IJobRegistrationStore
{
    private readonly List<JobDefinition> registrations = [];
    private readonly List<Type> globalBehaviorTypes = [];
    private IConfiguration configuration;
    private IServiceProvider serviceProvider;

    /// <summary>
    /// Adds a code-registered job definition.
    /// </summary>
    public void Add(JobDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (this.registrations.Any(x => string.Equals(x.JobName, definition.JobName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A job named '{definition.JobName}' is already registered.");
        }

        this.registrations.Add(definition);
    }

    /// <summary>
    /// Sets the scheduler configuration section used for overrides.
    /// </summary>
    public void SetConfiguration(IConfiguration value)
    {
        this.configuration = value;
    }

    /// <summary>
    /// Sets the root service provider used to resolve optional registration properties.
    /// </summary>
    public void SetServiceProvider(IServiceProvider value)
    {
        this.serviceProvider = value;
    }

    /// <summary>
    /// Adds a global behavior type.
    /// </summary>
    public void AddGlobalBehavior(Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);

        if (!typeof(IJobBehavior).IsAssignableFrom(behaviorType) || behaviorType.IsAbstract)
        {
            throw new InvalidOperationException($"The behavior type '{behaviorType.FullName}' must implement {nameof(IJobBehavior)}.");
        }

        if (!this.globalBehaviorTypes.Contains(behaviorType))
        {
            this.globalBehaviorTypes.Add(behaviorType);
        }
    }

    /// <summary>
    /// Gets the global behavior types in registration order.
    /// </summary>
    public IReadOnlyList<Type> GetGlobalBehaviorTypes() => this.globalBehaviorTypes.ToArray();

    /// <summary>
    /// Gets the resolved job definitions after applying matching appsettings overrides.
    /// </summary>
    public IReadOnlyList<JobDefinition> GetDefinitions()
    {
        var resolved = this.registrations
            .Select(x => x with
            {
                Properties = x.Properties.Clone(),
                TargetInstances = x.TargetInstances.ToArray(),
                Triggers = x.Triggers.Select(t => t with { Properties = t.Properties.Clone() }).ToArray(),
                Chains = x.Chains.Select(c => c with
                {
                    Properties = c.Properties.Clone(),
                    RequiredStatuses = c.RequiredStatuses.ToArray(),
                }).ToArray(),
                BehaviorTypes = x.BehaviorTypes.ToArray(),
            })
            .ToArray();

        for (var i = 0; i < resolved.Length; i++)
        {
            var moduleName = !string.IsNullOrWhiteSpace(resolved[i].Module)
                ? resolved[i].Module
                : this.ResolveModuleName(resolved[i].JobType);
            var displayName = resolved[i].HasExplicitDisplayName
                ? resolved[i].DisplayName
                : JobNamingConventions.ResolveDisplayName(resolved[i].JobType, moduleName);

            resolved[i] = resolved[i] with
            {
                Module = moduleName,
                DisplayName = displayName,
            };
        }

        var schedulerConfiguration = JobConfiguration.Parse(this.configuration);
        foreach (var (jobName, overrideDefinition) in schedulerConfiguration.Jobs)
        {
            var jobIndex = Array.FindIndex(resolved, x => string.Equals(x.JobName, jobName, StringComparison.OrdinalIgnoreCase));
            if (jobIndex < 0)
            {
                throw new InvalidOperationException($"The configuration contains an unknown job '{jobName}'. Appsettings cannot create jobs.");
            }

            var job = resolved[jobIndex];
            var triggers = job.Triggers.ToArray();
            foreach (var (triggerName, triggerOverride) in overrideDefinition.Triggers)
            {
                var triggerIndex = Array.FindIndex(triggers, x => string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase));
                if (triggerIndex < 0)
                {
                    throw new InvalidOperationException($"The configuration contains an unknown trigger '{triggerName}' for job '{jobName}'. Appsettings cannot create triggers.");
                }

                triggers[triggerIndex] = triggers[triggerIndex] with
                {
                    Enabled = triggerOverride.Enabled ?? triggers[triggerIndex].Enabled,
                    Schedule = string.IsNullOrWhiteSpace(triggerOverride.Schedule) ? triggers[triggerIndex].Schedule : triggerOverride.Schedule,
                    TargetInstances = triggerOverride.TargetInstances.Count > 0 ? triggerOverride.TargetInstances : triggers[triggerIndex].TargetInstances,
                };
            }

            resolved[jobIndex] = job with
            {
                Enabled = overrideDefinition.Enabled ?? job.Enabled,
                TargetInstances = overrideDefinition.TargetInstances.Count > 0 ? overrideDefinition.TargetInstances : job.TargetInstances,
                Triggers = triggers,
            };
        }

        ValidateChains(resolved);

        return resolved;
    }

    private string ResolveModuleName(Type jobType)
    {
        if (jobType is null || this.serviceProvider is null)
        {
            return null;
        }

        var module = this.serviceProvider.GetServices<IModuleContextAccessor>().Find(jobType);
        return string.IsNullOrWhiteSpace(module?.Name) ? null : module.Name;
    }

    private static void ValidateChains(IReadOnlyList<JobDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            foreach (var chain in definition.Chains)
            {
                var successor = definitions.FirstOrDefault(x => string.Equals(x.JobName, chain.SuccessorJobName, StringComparison.OrdinalIgnoreCase));
                if (successor is null)
                {
                    throw new InvalidOperationException($"The job '{definition.JobName}' chains to unknown job '{chain.SuccessorJobName}'.");
                }

                var trigger = ResolveChainTrigger(successor, chain);
                if (trigger.TriggerType != JobTriggerType.Manual)
                {
                    throw new InvalidOperationException($"The job '{definition.JobName}' chains to trigger '{trigger.TriggerName}' on job '{successor.JobName}', but only manual successor triggers are supported in the current chaining phase.");
                }
            }
        }
    }

    private static JobTriggerDefinition ResolveChainTrigger(JobDefinition successor, JobChainDefinition chain)
    {
        if (!string.IsNullOrWhiteSpace(chain.SuccessorTriggerName))
        {
            return successor.Triggers.FirstOrDefault(x => string.Equals(x.TriggerName, chain.SuccessorTriggerName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"The chained successor job '{successor.JobName}' does not define trigger '{chain.SuccessorTriggerName}'.");
        }

        var manualTriggers = successor.Triggers.Where(x => x.TriggerType == JobTriggerType.Manual).ToArray();
        return manualTriggers.Length switch
        {
            1 => manualTriggers[0],
            0 => throw new InvalidOperationException($"The chained successor job '{successor.JobName}' requires exactly one manual trigger when no successor trigger name is configured."),
            _ => throw new InvalidOperationException($"The chained successor job '{successor.JobName}' has multiple manual triggers. Configure the successor trigger explicitly."),
        };
    }
}
