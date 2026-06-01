// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides common job-definition configuration for outbound integration jobs.
/// </summary>
/// <typeparam name="TBuilder">The concrete builder type.</typeparam>
/// <typeparam name="TJob">The integration job type.</typeparam>
/// <typeparam name="TData">The typed job data contract.</typeparam>
public abstract class JobOutboundIntegrationDefinitionBuilderBase<TBuilder, TJob, TData>
    where TBuilder : JobOutboundIntegrationDefinitionBuilderBase<TBuilder, TJob, TData>
    where TJob : class, IJob
{
    private readonly JobDefinitionBuilder<TJob> jobBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobOutboundIntegrationDefinitionBuilderBase{TBuilder, TJob, TData}"/> class.
    /// </summary>
    /// <param name="jobName">The stable job name.</param>
    protected JobOutboundIntegrationDefinitionBuilderBase(string jobName)
    {
        this.JobName = string.IsNullOrWhiteSpace(jobName)
            ? throw new InvalidOperationException("A job name is required.")
            : jobName.Trim();
        this.jobBuilder = new JobDefinitionBuilder<TJob>(this.JobName).WithData<TData>();
    }

    /// <summary>
    /// Gets the stable job name.
    /// </summary>
    public string JobName { get; }

    /// <summary>
    /// Sets the display name.
    /// </summary>
    public TBuilder WithName(string value)
    {
        this.jobBuilder.Name(value);
        return this.Builder;
    }

    /// <summary>
    /// Sets the required description.
    /// </summary>
    public TBuilder WithDescription(string value)
    {
        this.jobBuilder.Description(value);
        return this.Builder;
    }

    /// <summary>
    /// Sets the optional group.
    /// </summary>
    public TBuilder Group(string value)
    {
        this.jobBuilder.Group(value);
        return this.Builder;
    }

    /// <summary>
    /// Sets the optional module.
    /// </summary>
    public TBuilder Module(string value)
    {
        this.jobBuilder.Module(value);
        return this.Builder;
    }

    /// <summary>
    /// Sets the effective enabled state.
    /// </summary>
    public TBuilder Enabled(bool value = true)
    {
        this.jobBuilder.Enabled(value);
        return this.Builder;
    }

    /// <summary>
    /// Sets the default priority.
    /// </summary>
    public TBuilder WithPriority(int value)
    {
        this.jobBuilder.WithPriority(value);
        return this.Builder;
    }

    /// <summary>
    /// Sets the default timeout.
    /// </summary>
    public TBuilder WithTimeout(TimeSpan value)
    {
        this.jobBuilder.WithTimeout(value);
        return this.Builder;
    }

    /// <summary>
    /// Sets the default concurrency options.
    /// </summary>
    public TBuilder WithConcurrency(int limit)
    {
        this.jobBuilder.WithConcurrency(limit);
        return this.Builder;
    }

    /// <summary>
    /// Sets properties on the job definition.
    /// </summary>
    public TBuilder WithProperty(string key, string value)
    {
        this.jobBuilder.WithProperty(key, value);
        return this.Builder;
    }

    /// <summary>
    /// Sets the retry policy.
    /// </summary>
    public TBuilder WithRetry(Action<JobRetryPolicyBuilder> configure)
    {
        this.jobBuilder.WithRetry(configure);
        return this.Builder;
    }

    /// <summary>
    /// Adds a trigger definition.
    /// </summary>
    public TBuilder AddTrigger(string triggerName, Action<JobTriggerDefinitionBuilder> configure)
    {
        this.jobBuilder.AddTrigger(triggerName, configure);
        return this.Builder;
    }

    /// <summary>
    /// Adds a chained successor occurrence template.
    /// </summary>
    public TBuilder Then(string successorJobName, Action<JobChainDefinitionBuilder> configure = null)
    {
        this.jobBuilder.Then(successorJobName, configure);
        return this.Builder;
    }

    /// <summary>
    /// Builds the immutable job definition.
    /// </summary>
    public JobDefinition BuildDefinition() => this.jobBuilder.Build();

    private TBuilder Builder => (TBuilder)this;
}