// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Builds a lightweight inline job definition backed by a delegate.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddJobScheduler()
///     .WithJob("cleanup-inline", job =&gt; job
///         .WithDescription("Runs inline cleanup logic.")
///         .Execute((context, cancellationToken) =&gt; Task.FromResult(Result.Success()))
///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
/// </code>
/// </example>
public sealed class InlineJobDefinitionBuilder
{
    private readonly JobDefinitionBuilder<InlineJobRuntime> inner;
    private Func<IJobExecutionContext, IServiceProvider, CancellationToken, Task<Result>> handler;

    internal InlineJobDefinitionBuilder(string jobName)
    {
        this.inner = new JobDefinitionBuilder<InlineJobRuntime>(jobName, allowExplicitDataContractOverride: true);
    }

    internal string JobName => this.inner.JobName;

    /// <summary>
    /// Executes the with name operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder WithName(string value)
    {
        this.inner.Name(value);
        return this;
    }

    /// <summary>
    /// Executes the with description operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder WithDescription(string value)
    {
        this.inner.Description(value);
        return this;
    }

    /// <summary>
    /// Executes the group operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder Group(string value)
    {
        this.inner.Group(value);
        return this;
    }

    /// <summary>
    /// Executes the module operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder Module(string value)
    {
        this.inner.Module(value);
        return this;
    }

    /// <summary>
    /// Executes the enabled operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public InlineJobDefinitionBuilder Enabled(bool value = true)
    {
        this.inner.Enabled(value);
        return this;
    }

    /// <summary>
    /// Executes the with priority operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder WithPriority(int value)
    {
        this.inner.WithPriority(value);
        return this;
    }

    /// <summary>
    /// Executes the with timeout operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder WithTimeout(TimeSpan value)
    {
        this.inner.WithTimeout(value);
        return this;
    }

    /// <summary>
    /// Executes the with concurrency operation.
    /// </summary>
    /// <param name="limit">The limit used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder WithConcurrency(int limit)
    {
        this.inner.WithConcurrency(limit);
        return this;
    }

    /// <summary>
    /// Executes the with data operation.
    /// </summary>
    /// <typeparam name="TData">The data type.</typeparam>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder WithData<TData>()
    {
        this.inner.WithData<TData>();
        return this;
    }

    /// <summary>
    /// Executes the with property operation.
    /// </summary>
    /// <param name="key">The key used by the operation.</param>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder WithProperty(string key, string value)
    {
        this.inner.WithProperty(key, value);
        return this;
    }

    /// <summary>
    /// Executes the target instances operation.
    /// </summary>
    /// <param name="values">The values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder TargetInstances(params string[] values)
    {
        this.inner.TargetInstances(values);
        return this;
    }

    /// <summary>
    /// Executes the with retry operation.
    /// </summary>
    /// <param name="configure">The delegate used to configure the component.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder WithRetry(Action<JobRetryPolicyBuilder> configure)
    {
        this.inner.WithRetry(configure);
        return this;
    }

    /// <summary>
    /// Provides with behavior.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior type.</typeparam>
    public InlineJobDefinitionBuilder WithBehavior<TBehavior>()
        where TBehavior : class, IJobBehavior
    {
        this.inner.WithBehavior<TBehavior>();
        return this;
    }

    /// <summary>
    /// Executes the with behavior operation.
    /// </summary>
    /// <param name="behaviorType">The behavior type used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder WithBehavior(Type behaviorType)
    {
        this.inner.WithBehavior(behaviorType);
        return this;
    }

    /// <summary>
    /// Adds trigger.
    /// </summary>
    /// <param name="triggerName">The trigger name used by the operation.</param>
    /// <param name="configure">The delegate used to configure the component.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder AddTrigger(
        string triggerName,
        Action<JobTriggerDefinitionBuilder> configure)
    {
        this.inner.AddTrigger(triggerName, configure);
        return this;
    }

    /// <summary>
    /// Executes the then operation.
    /// </summary>
    /// <param name="successorJobName">The successor job name used by the operation.</param>
    /// <param name="configure">The delegate used to configure the component.</param>
    /// <returns>The result of the operation.</returns>
    public InlineJobDefinitionBuilder Then(
        string successorJobName,
        Action<JobChainDefinitionBuilder> configure = null)
    {
        this.inner.Then(successorJobName, configure);
        return this;
    }

    /// <summary>
    /// Executes the execute operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public InlineJobDefinitionBuilder Execute(Func<IJobExecutionContext, CancellationToken, Task<Result>> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        this.handler = (context, _, cancellationToken) => value(context, cancellationToken);
        return this;
    }

    /// <summary>
    /// Executes the execute operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public InlineJobDefinitionBuilder Execute(Func<IJobExecutionContext, IServiceProvider, CancellationToken, Task<Result>> value)
    {
        this.handler = value ?? throw new ArgumentNullException(nameof(value));
        return this;
    }

    /// <summary>
    /// Executes the execute operation.
    /// </summary>
    /// <typeparam name="TData">The data type.</typeparam>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public InlineJobDefinitionBuilder Execute<TData>(Func<IJobExecutionContext<TData>, CancellationToken, Task<Result>> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        this.inner.WithData<TData>();
        this.handler = (context, _, cancellationToken) =>
        {
            if (context is not IJobExecutionContext<TData> typedContext)
            {
                return Task.FromResult(Result.Failure($"The inline job '{context.JobName}' expected data contract '{typeof(TData).FullName}'."));
            }

            return value(typedContext, cancellationToken);
        };

        return this;
    }

    /// <summary>
    /// Executes the execute operation.
    /// </summary>
    /// <typeparam name="TData">The data type.</typeparam>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public InlineJobDefinitionBuilder Execute<TData>(Func<IJobExecutionContext<TData>, IServiceProvider, CancellationToken, Task<Result>> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        this.inner.WithData<TData>();
        this.handler = (context, serviceProvider, cancellationToken) =>
        {
            if (context is not IJobExecutionContext<TData> typedContext)
            {
                return Task.FromResult(Result.Failure($"The inline job '{context.JobName}' expected data contract '{typeof(TData).FullName}'."));
            }

            return value(typedContext, serviceProvider, cancellationToken);
        };

        return this;
    }

    internal JobDefinition Build()
    {
        if (this.handler is null)
        {
            throw new InvalidOperationException($"The inline job '{this.JobName}' requires an execution delegate.");
        }

        return this.inner.Build();
    }

    internal Func<IJobExecutionContext, IServiceProvider, CancellationToken, Task<Result>> GetHandler()
    {
        if (this.handler is null)
        {
            throw new InvalidOperationException($"The inline job '{this.JobName}' requires an execution delegate.");
        }

        return this.handler;
    }
}
