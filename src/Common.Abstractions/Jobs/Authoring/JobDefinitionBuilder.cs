// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builds a code-first job definition.
/// </summary>
/// <typeparam name="TJob">The job type.</typeparam>
/// <example>
/// <code>
/// var builder = new JobDefinitionBuilder&lt;CleanupJob&gt;("cleanup");
/// builder.WithDescription("Removes stale records.")
///     .AddTrigger("manual", trigger =&gt; trigger.Manual());
/// </code>
/// </example>
public class JobDefinitionBuilder<TJob>
    where TJob : class, IJob
{
    private readonly bool allowExplicitDataContractOverride;
    private readonly List<JobTriggerDefinition> triggers = [];
    private readonly List<JobChainDefinition> chains = [];
    private readonly List<Type> behaviorTypes = [];
    private readonly Dictionary<string, string> properties = [];
    private readonly List<string> targetInstances = [];
    private Type explicitDataType;
    private string displayName;
    private string description;
    private string group = JobDefinition.DefaultGroup;
    private string module;
    private bool enabled = true;
    private ServiceLifetime lifetime = ServiceLifetime.Transient;
    private int priority;
    private TimeSpan? timeout;
    private JobRetryPolicy retryPolicy;
    private JobConcurrencyOptions concurrency = JobConcurrencyOptions.Default;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobDefinitionBuilder{TJob}"/> class.
    /// </summary>
    /// <param name="jobName">The stable job name.</param>
    public JobDefinitionBuilder(string jobName)
        : this(jobName, false)
    {
    }

    public JobDefinitionBuilder(string jobName, bool allowExplicitDataContractOverride)
    {
        this.JobName = string.IsNullOrWhiteSpace(jobName)
            ? throw new InvalidOperationException("A job name is required.")
            : jobName.Trim();
        this.allowExplicitDataContractOverride = allowExplicitDataContractOverride;
    }

    /// <summary>
    /// Gets the stable job name.
    /// </summary>
    public string JobName { get; }

    /// <summary>
    /// Sets the display name.
    /// </summary>
    public JobDefinitionBuilder<TJob> Name(string value)
    {
        this.displayName = string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"The job '{this.JobName}' requires a non-empty display name when configured explicitly.") : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the optional description.
    /// </summary>
    public JobDefinitionBuilder<TJob> Description(string value)
    {
        this.description = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the optional group.
    /// </summary>
    public JobDefinitionBuilder<TJob> Group(string value)
    {
        this.group = string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"The job '{this.JobName}' requires a non-empty group when configured explicitly.") : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the optional module.
    /// </summary>
    public JobDefinitionBuilder<TJob> Module(string value)
    {
        this.module = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the effective enabled state.
    /// </summary>
    public JobDefinitionBuilder<TJob> Enabled(bool value = true)
    {
        this.enabled = value;
        return this;
    }

    /// <summary>
    /// Sets the job service lifetime.
    /// </summary>
    public JobDefinitionBuilder<TJob> UseLifetime(ServiceLifetime value)
    {
        if (value is not (ServiceLifetime.Transient or ServiceLifetime.Scoped or ServiceLifetime.Singleton))
        {
            throw new InvalidOperationException($"The job '{this.JobName}' requires a supported service lifetime.");
        }

        this.lifetime = value;
        return this;
    }

    /// <summary>
    /// Sets the default priority.
    /// </summary>
    public JobDefinitionBuilder<TJob> WithPriority(int value)
    {
        this.priority = value;
        return this;
    }

    /// <summary>
    /// Sets the default timeout.
    /// </summary>
    public JobDefinitionBuilder<TJob> WithTimeout(TimeSpan value)
    {
        this.timeout = value;
        return this;
    }

    /// <summary>
    /// Sets the default concurrency options.
    /// </summary>
    public JobDefinitionBuilder<TJob> WithConcurrency(int limit)
    {
        this.concurrency = new JobConcurrencyOptions(limit);
        return this;
    }

    /// <summary>
    /// Sets the explicit data contract.
    /// </summary>
    public JobDefinitionBuilder<TJob> WithData<TData>()
    {
        this.explicitDataType = typeof(TData);
        return this;
    }

    /// <summary>
    /// Sets properties on the job definition.
    /// </summary>
    public JobDefinitionBuilder<TJob> WithProperty(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException($"The job '{this.JobName}' requires non-empty property keys.");
        }

        this.properties[key.Trim()] = value ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Sets the eligible scheduler instance targets for this job.
    /// </summary>
    public JobDefinitionBuilder<TJob> TargetInstances(params string[] values)
    {
        this.targetInstances.Clear();
        foreach (var value in values.SafeNull())
        {
            var target = value?.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            if (!this.targetInstances.Contains(target, StringComparer.OrdinalIgnoreCase))
            {
                this.targetInstances.Add(target);
            }
        }

        if (values is not null && values.Length > 0 && this.targetInstances.Count == 0)
        {
            throw new InvalidOperationException($"The job '{this.JobName}' requires at least one non-empty scheduler instance target when targeting is configured.");
        }

        return this;
    }

    /// <summary>
    /// Sets the retry policy.
    /// </summary>
    public JobDefinitionBuilder<TJob> WithRetry(Action<JobRetryPolicyBuilder> configure)
    {
        var builder = new JobRetryPolicyBuilder();
        configure?.Invoke(builder);
        this.retryPolicy = builder.Build();
        return this;
    }

    /// <summary>
    /// Adds a job behavior.
    /// </summary>
    public JobDefinitionBuilder<TJob> WithBehavior<TBehavior>()
        where TBehavior : class, IJobBehavior
        => this.WithBehavior(typeof(TBehavior));

    /// <summary>
    /// Adds a job behavior.
    /// </summary>
    public JobDefinitionBuilder<TJob> WithBehavior(Type behaviorType)
    {
        if (behaviorType is null)
        {
            throw new ArgumentNullException(nameof(behaviorType));
        }

        if (!typeof(IJobBehavior).IsAssignableFrom(behaviorType) || behaviorType.IsAbstract)
        {
            throw new InvalidOperationException($"The job '{this.JobName}' requires behavior '{behaviorType.FullName}' to implement {nameof(IJobBehavior)}.");
        }

        if (!this.behaviorTypes.Contains(behaviorType))
        {
            this.behaviorTypes.Add(behaviorType);
        }

        return this;
    }

    /// <summary>
    /// Adds a trigger definition.
    /// </summary>
    public JobDefinitionBuilder<TJob> AddTrigger(
        string triggerName,
        Action<JobTriggerDefinitionBuilder> configure)
    {
        var builder = new JobTriggerDefinitionBuilder(triggerName, this.JobName, this.GetResolvedDataType());
        configure?.Invoke(builder);

        var definition = builder.Build();
        if (this.triggers.Any(x => string.Equals(x.TriggerName, definition.TriggerName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"The job '{this.JobName}' already contains a trigger named '{definition.TriggerName}'.");
        }

        this.triggers.Add(definition);
        return this;
    }

    /// <summary>
    /// Adds a chained successor occurrence template.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Then("index-orders", chain =&gt; chain.WithTrigger("manual"));
    /// </code>
    /// </example>
    public JobDefinitionBuilder<TJob> Then(
        string successorJobName,
        Action<JobChainDefinitionBuilder> configure = null)
    {
        var builder = new JobChainDefinitionBuilder(this.JobName, successorJobName);
        configure?.Invoke(builder);
        this.chains.Add(builder.Build());

        return this;
    }

    /// <summary>
    /// Builds the immutable job definition.
    /// </summary>
    public JobDefinition Build()
    {
        var resolvedDataType = this.GetResolvedDataType();

        if (this.triggers.Count == 0)
        {
            throw new InvalidOperationException($"The job '{this.JobName}' requires at least one trigger.");
        }

        return new JobDefinition
        {
            JobName = this.JobName,
            DisplayName = string.IsNullOrWhiteSpace(this.displayName) ? JobNamingConventions.ResolveDisplayName(typeof(TJob), this.module) : this.displayName,
            HasExplicitDisplayName = !string.IsNullOrWhiteSpace(this.displayName),
            Description = this.description,
            JobType = typeof(TJob),
            Group = string.IsNullOrWhiteSpace(this.group) ? JobDefinition.DefaultGroup : this.group,
            Module = this.module,
            Enabled = this.enabled,
            Lifetime = this.lifetime,
            Priority = this.priority,
            Timeout = this.timeout,
            RetryPolicy = this.retryPolicy,
            Concurrency = this.concurrency,
            DataType = resolvedDataType,
            Properties = new PropertyBag(this.properties.ToDictionary(x => x.Key, x => (object)x.Value, StringComparer.OrdinalIgnoreCase)),
            TargetInstances = this.targetInstances.ToArray(),
            Triggers = this.triggers.ToArray(),
            Chains = this.chains.ToArray(),
            BehaviorTypes = this.ResolveBehaviorTypes(),
        };
    }

    private Type[] ResolveBehaviorTypes()
    {
        var seen = new HashSet<Type>();
        var resolved = new List<Type>();

        foreach (var attribute in typeof(TJob).GetCustomAttributes(typeof(WithBehaviorAttribute), true).OfType<WithBehaviorAttribute>())
        {
            if (seen.Add(attribute.BehaviorType))
            {
                resolved.Add(attribute.BehaviorType);
            }
        }

        foreach (var behaviorType in this.behaviorTypes)
        {
            if (seen.Add(behaviorType))
            {
                resolved.Add(behaviorType);
            }
        }

        return resolved.ToArray();
    }

    private Type GetResolvedDataType()
    {
        var inferredDataType = JobDataContractResolver.Resolve(typeof(TJob));
        if (this.allowExplicitDataContractOverride && this.explicitDataType is not null)
        {
            return this.explicitDataType;
        }

        if (this.explicitDataType is not null && inferredDataType != this.explicitDataType)
        {
            throw new InvalidOperationException($"The job '{this.JobName}' declares '{inferredDataType.FullName}' through its type but was configured with '{this.explicitDataType.FullName}'.");
        }

        return this.explicitDataType ?? inferredDataType;
    }
}
