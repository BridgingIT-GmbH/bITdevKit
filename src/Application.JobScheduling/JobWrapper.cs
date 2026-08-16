// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

/// <summary>
/// Represents job wrapper.
/// </summary>
/// <param name="serviceProvider">The service provider used by the operation.</param>
/// <param name="innerJob">The inner job used by the operation.</param>
/// <param name="moduleAccessors">The module accessors used by the operation.</param>
public class JobWrapper(
    IServiceProvider serviceProvider,
    IJob innerJob,
    IEnumerable<IModuleContextAccessor> moduleAccessors) : IJob, IDisposable
{
    private const string CorrelationKey = "CorrelationId";
    private const string FlowKey = "FlowId";
    private const string JobIdKey = "JobId";
    private const string JobTypeKey = "JobType";
    private readonly IServiceProvider serviceProvider = serviceProvider;

    /// <summary>
    /// Gets or sets the inner job.
    /// </summary>
    public IJob InnerJob { get; set; } = innerJob;

    /// <summary>
    /// Gets or sets the module accessors.
    /// </summary>
    public IEnumerable<IModuleContextAccessor> ModuleAccessors { get; set; } = moduleAccessors;

    /// <summary>
    /// Executes the execute operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task Execute(IJobExecutionContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        var logger = this.serviceProvider?.GetService<ILoggerFactory>()?.CreateLogger(this.GetType());
        context.Trigger.JobDataMap.TryGetString(Constants.CorrelationIdKey, out var triggerCorrelationId);
        var correlationId = triggerCorrelationId.EmptyToNull() ?? GuidGenerator.CreateSequential().ToString("N");
        var flowId = GuidGenerator.Create(this.GetType().ToString()).ToString("N");
        var jobId = context.JobDetail.JobDataMap.GetString(JobIdKey) ?? context.FireInstanceId;
        var jobTypeName = context.JobDetail.JobType.FullName;

        using (logger?.BeginScope(new Dictionary<string, object>
        {
            [CorrelationKey] = correlationId,
            [FlowKey] = flowId,
            [JobIdKey] = jobId,
            [JobTypeKey] = jobTypeName
        }))
        {
            try
            {
                var behaviors = this.serviceProvider?.GetServices<IJobSchedulingBehavior>();
                logger?.LogDebug($"{{LogKey}} behaviors: {behaviors.SafeNull().Select(b => b.GetType().Name).ToString(" -> ")} -> {this.GetType().Name}:Execute", Constants.LogKey);
                // Activity.Current?.AddEvent(new($"behaviours: {behaviors.SafeNull().Select(b => b.GetType().Name).ToString(" -> ")} -> {this.GetType().Name}:Execute"));

                context.Put("ModuleContextAccessors", this.ModuleAccessors);
                context.Put(Constants.CorrelationIdKey, correlationId);
                context.Put(Constants.FlowIdKey, flowId);
                context.Trigger.JobDataMap.TryGetString(Constants.TriggeredByKey, out var triggeredBy);
                context.Put(Constants.TriggeredByKey, triggeredBy.EmptyToNull() ?? context.Scheduler.SchedulerName);

                await this.ExecutePipelineAsync(context, behaviors);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[{LogKey}] processing error (type={JobType}, id={JobId}): {ErrorMessage}", Constants.LogKey, jobTypeName, jobId, ex.Message);
            }
        }
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public virtual void Dispose()
    {
        (this.InnerJob as IDisposable)?.Dispose();
    }

    private async Task ExecutePipelineAsync(IJobExecutionContext context, IEnumerable<IJobSchedulingBehavior> behaviors)
    {
        async Task JobExecutor()
        {
            await this.InnerJob.Execute(context).AnyContext();
        }

        await behaviors.SafeNull()
            .Reverse()
            .Aggregate((JobDelegate)JobExecutor,
                (next, pipeline) => async () => await pipeline.Execute(context, next))();
    }
}
