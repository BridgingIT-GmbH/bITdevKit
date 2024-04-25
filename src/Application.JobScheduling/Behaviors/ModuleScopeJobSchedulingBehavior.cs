// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Diagnostics;

public class ModuleScopeJobSchedulingBehavior : JobSchedulingBehaviorBase
{
    private readonly IEnumerable<ActivitySource> activitySources;

    public ModuleScopeJobSchedulingBehavior(
        ILoggerFactory loggerFactory,
        IEnumerable<ActivitySource> activitySources = null)
        : base(loggerFactory)
    {
        this.activitySources = activitySources;
    }

    public override async Task Execute(IJobExecutionContext context, JobDelegate next)
    {
        var moduleAccessors = context.Get("ModuleContextAccessors") as IEnumerable<IModuleContextAccessor>;
        var module = moduleAccessors.Find(context.JobDetail.JobType);

        using (this.Logger.BeginScope(new Dictionary<string, object>
        {
            [ModuleConstants.ModuleNameKey] = module?.Name ?? ModuleConstants.UnknownModuleName
        }))
        {
            if (module is not null && !module.Enabled)
            {
                throw new ModuleNotEnabledException(module.Name);
            }
            else
            {
                var jobId = context.JobDetail.JobDataMap?.GetString(Constants.JobIdKey) ?? context.FireInstanceId;
                var jobType = context.JobDetail.JobType.Name;
                var correlationId = context.Get(Constants.CorrelationIdKey) as string;
                var flowId = context.Get(Constants.FlowIdKey) as string;

                await this.activitySources.Find(module?.Name).StartActvity(
                    $"MODULE {module?.Name}",
                    async (a, c) =>
                    {
                        using (this.Logger.BeginScope(new Dictionary<string, object>
                        {
                            [Constants.TraceIdKey] = a.TraceId.ToString(),
                        }))
                        {
                            await this.activitySources.Find(module?.Name).StartActvity(
                                $"JOB_EXECUTE {context.JobDetail.JobType.Name}",
                                async (a, c) => await next().AnyContext(),
                                tags: new Dictionary<string, string>
                                {
                                    ["job.id"] = jobId,
                                    ["job.type"] = jobType
                                }, cancellationToken: c);
                        }
                    },
                    baggages: new Dictionary<string, string>
                    {
                        [ActivityConstants.ModuleNameTagKey] = module?.Name,
                        [ActivityConstants.CorrelationIdTagKey] = correlationId,
                        [ActivityConstants.FlowIdTagKey] = flowId,
                    });
            }
        }
    }
}