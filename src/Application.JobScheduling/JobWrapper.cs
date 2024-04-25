// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class JobWrapper : IJob, IDisposable
{
    private const string CorrelationKey = "CorrelationId";
    private const string FlowKey = "FlowId";
    private const string JobIdKey = "JobId";
    private const string JobTypeKey = "JobType";
    private readonly IServiceProvider serviceProvider;

    public JobWrapper(
        IServiceProvider serviceProvider,
        IJob innerJob,
        IEnumerable<IModuleContextAccessor> moduleAccessors)
    {
        this.serviceProvider = serviceProvider;
        this.InnerJob = innerJob;
        this.ModuleAccessors = moduleAccessors;
    }

    public IJob InnerJob { get; set; }

    public IEnumerable<IModuleContextAccessor> ModuleAccessors { get; set; }

    public virtual async Task Execute(IJobExecutionContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        var logger = this.serviceProvider?.GetService<ILoggerFactory>()?.CreateLogger(this.GetType());
        var jobId = context.JobDetail.JobDataMap.GetString(JobIdKey) ?? context.FireInstanceId;
        var jobTypeName = context.JobDetail.JobType.Name;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationKey] = GuidGenerator.CreateSequential().ToString("N"),
            [FlowKey] = GuidGenerator.Create(this.GetType().ToString()).ToString("N"),
            [JobIdKey] = jobId,
            [JobTypeKey] = jobTypeName,
        }))
        {
            try
            {
                var behaviors = this.serviceProvider?.GetServices<IJobSchedulingBehavior>();
                logger?.LogDebug($"{{LogKey}} behaviors: {behaviors.SafeNull().Select(b => b.GetType().Name).ToString(" -> ")} -> {this.GetType().Name}:Execute", Constants.LogKey);
                // Activity.Current?.AddEvent(new($"behaviours: {behaviors.SafeNull().Select(b => b.GetType().Name).ToString(" -> ")} -> {this.GetType().Name}:Execute"));

                context.Put("ModuleContextAccessors", this.ModuleAccessors);

                async Task JobExecutor() => await this.InnerJob.Execute(context).AnyContext();
                await behaviors.SafeNull().Reverse()
                  .Aggregate((JobDelegate)JobExecutor, (next, pipeline) => async () =>
                      await pipeline.Execute(context, next))();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{LogKey} processing error (type={JobType}, id={JobId}): {ErrorMessage}", Constants.LogKey, jobTypeName, jobId, ex.Message);
            }
        }
    }

    public virtual void Dispose()
    {
        (this.InnerJob as IDisposable)?.Dispose();
    }
}