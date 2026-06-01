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

    public InlineJobDefinitionBuilder WithName(string value)
    {
        this.inner.Name(value);
        return this;
    }

    public InlineJobDefinitionBuilder WithDescription(string value)
    {
        this.inner.Description(value);
        return this;
    }

    public InlineJobDefinitionBuilder Group(string value)
    {
        this.inner.Group(value);
        return this;
    }

    public InlineJobDefinitionBuilder Module(string value)
    {
        this.inner.Module(value);
        return this;
    }

    public InlineJobDefinitionBuilder Enabled(bool value = true)
    {
        this.inner.Enabled(value);
        return this;
    }

    public InlineJobDefinitionBuilder WithPriority(int value)
    {
        this.inner.WithPriority(value);
        return this;
    }

    public InlineJobDefinitionBuilder WithTimeout(TimeSpan value)
    {
        this.inner.WithTimeout(value);
        return this;
    }

    public InlineJobDefinitionBuilder WithConcurrency(int limit)
    {
        this.inner.WithConcurrency(limit);
        return this;
    }

    public InlineJobDefinitionBuilder WithData<TData>()
    {
        this.inner.WithData<TData>();
        return this;
    }

    public InlineJobDefinitionBuilder WithProperty(string key, string value)
    {
        this.inner.WithProperty(key, value);
        return this;
    }

    public InlineJobDefinitionBuilder TargetInstances(params string[] values)
    {
        this.inner.TargetInstances(values);
        return this;
    }

    public InlineJobDefinitionBuilder WithRetry(Action<JobRetryPolicyBuilder> configure)
    {
        this.inner.WithRetry(configure);
        return this;
    }

    public InlineJobDefinitionBuilder WithBehavior<TBehavior>()
        where TBehavior : class, IJobBehavior
    {
        this.inner.WithBehavior<TBehavior>();
        return this;
    }

    public InlineJobDefinitionBuilder WithBehavior(Type behaviorType)
    {
        this.inner.WithBehavior(behaviorType);
        return this;
    }

    public InlineJobDefinitionBuilder AddTrigger(
        string triggerName,
        Action<JobTriggerDefinitionBuilder> configure)
    {
        this.inner.AddTrigger(triggerName, configure);
        return this;
    }

    public InlineJobDefinitionBuilder Then(
        string successorJobName,
        Action<JobChainDefinitionBuilder> configure = null)
    {
        this.inner.Then(successorJobName, configure);
        return this;
    }

    public InlineJobDefinitionBuilder Execute(Func<IJobExecutionContext, CancellationToken, Task<Result>> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        this.handler = (context, _, cancellationToken) => value(context, cancellationToken);
        return this;
    }

    public InlineJobDefinitionBuilder Execute(Func<IJobExecutionContext, IServiceProvider, CancellationToken, Task<Result>> value)
    {
        this.handler = value ?? throw new ArgumentNullException(nameof(value));
        return this;
    }

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
