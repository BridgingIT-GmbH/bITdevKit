// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents the next delegate in the job behavior pipeline.
/// </summary>
public delegate Task<IResult<JobExecutionResult>> JobBehaviorDelegate();

/// <summary>
/// Decorates job execution with cross-cutting behavior.
/// </summary>
/// <example>
/// <code>
/// public sealed class AuditJobBehavior : IJobBehavior
/// {
///     public async Task&lt;Result&lt;JobExecutionResult&gt;&gt; HandleAsync(
///         JobBehaviorContext context,
///         JobBehaviorDelegate next,
///         CancellationToken cancellationToken = default)
///     {
///         context.ExecutionContext.Messages.Add($"Entering {context.JobName}.");
///         return await next();
///     }
/// }
/// </code>
/// </example>
public interface IJobBehavior
{
    /// <summary>
    /// Handles the current job execution and optionally delegates to the next behavior.
    /// </summary>
    Task<IResult<JobExecutionResult>> HandleAsync(
        JobBehaviorContext context,
        JobBehaviorDelegate next,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides runtime information to a job behavior.
/// </summary>
/// <example>
/// <code>
/// public sealed class SampleJobBehavior : IJobBehavior
/// {
///     public Task&lt;Result&lt;JobExecutionResult&gt;&gt; HandleAsync(
///         JobBehaviorContext context,
///         JobBehaviorDelegate next,
///         CancellationToken cancellationToken = default)
///     {
///         context.ExecutionContext.Properties["sample"] = true;
///         return next();
///     }
/// }
/// </code>
/// </example>
public sealed class JobBehaviorContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobBehaviorContext"/> class.
    /// </summary>
    public JobBehaviorContext(
        IServiceProvider services,
        JobDefinition definition,
        JobTriggerDefinition trigger,
        IJob job,
        IJobExecutionContext executionContext,
        string schedulerInstanceId,
        int activeExecutionCount,
        int maxConcurrency)
    {
        this.Services = services ?? throw new ArgumentNullException(nameof(services));
        this.Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        this.Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        this.Job = job ?? throw new ArgumentNullException(nameof(job));
        this.ExecutionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        this.SchedulerInstanceId = schedulerInstanceId;
        this.ActiveExecutionCount = activeExecutionCount;
        this.MaxConcurrency = maxConcurrency;
    }

    /// <summary>
    /// Gets the scoped service provider for the execution.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the resolved job definition.
    /// </summary>
    public JobDefinition Definition { get; }

    /// <summary>
    /// Gets the originating trigger definition.
    /// </summary>
    public JobTriggerDefinition Trigger { get; }

    /// <summary>
    /// Gets the resolved job instance.
    /// </summary>
    public IJob Job { get; }

    /// <summary>
    /// Gets the execution context.
    /// </summary>
    public IJobExecutionContext ExecutionContext { get; }

    /// <summary>
    /// Gets the executing job type.
    /// </summary>
    public Type JobType => this.Definition.JobType;

    /// <summary>
    /// Gets the stable job name.
    /// </summary>
    public string JobName => this.ExecutionContext.JobName;

    /// <summary>
    /// Gets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; }

    /// <summary>
    /// Gets the active execution count at pipeline entry.
    /// </summary>
    public int ActiveExecutionCount { get; }

    /// <summary>
    /// Gets the configured scheduler concurrency ceiling.
    /// </summary>
    public int MaxConcurrency { get; }
}
