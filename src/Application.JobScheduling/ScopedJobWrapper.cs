// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

public class ScopedJobWrapper(IServiceScope scope, IJob innerJob, IEnumerable<IModuleContextAccessor> moduleAccessors)
    : JobWrapper(null, innerJob, moduleAccessors)
{
    private readonly IServiceScope scope = scope;

    public override async Task Execute(IJobExecutionContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        context.Trigger.JobDataMap.TryGetString(Constants.CorrelationIdKey, out var triggerCorrelationId);
        var correlationId = triggerCorrelationId.EmptyToNull() ?? GuidGenerator.CreateSequential().ToString("N");
        var flowId = GuidGenerator.Create(this.GetType().ToString()).ToString("N");
        var logger = this.scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger(this.GetType());
        var jobId = context.JobDetail.JobDataMap?.GetString(Constants.JobIdKey) ?? context.FireInstanceId;
        var jobTypeName = context.JobDetail.JobType.FullName;

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   [Constants.CorrelationIdKey] = correlationId,
                   [Constants.FlowIdKey] = flowId,
                   [Constants.JobIdKey] = jobId,
                   [Constants.JobTypeKey] = jobTypeName
               }))
        {
            try
            {
                var behaviors = this.scope.ServiceProvider.GetServices<IJobSchedulingBehavior>();
                logger?.LogDebug(
                    $"{{LogKey}} behaviors: {behaviors.SafeNull().Select(b => b.GetType().Name).ToString(" -> ")} -> {this.GetType().Name}:Execute",
                    Constants.LogKey);

                context.Put("ModuleContextAccessors", this.ModuleAccessors);
                context.Put(Constants.CorrelationIdKey, correlationId);
                context.Put(Constants.FlowIdKey, flowId);
                context.Trigger.JobDataMap.TryGetString(Constants.TriggeredByKey, out var triggeredBy);
                context.Put(Constants.TriggeredByKey, triggeredBy.EmptyToNull() ?? context.Scheduler.SchedulerName);

                await this.ExecutePipeline(context, behaviors);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "{LogKey} processing error (type={JobType}, id={JobId}): {ErrorMessage}",
                    Constants.LogKey,
                    jobTypeName,
                    jobId,
                    ex.Message);
            }
        }
    }

    public override void Dispose()
    {
        this.scope?.Dispose();
        base.Dispose();
    }

    private async Task ExecutePipeline(IJobExecutionContext context, IEnumerable<IJobSchedulingBehavior> behaviors)
    {
        // create a behavior pipeline and run it (execute > next)
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