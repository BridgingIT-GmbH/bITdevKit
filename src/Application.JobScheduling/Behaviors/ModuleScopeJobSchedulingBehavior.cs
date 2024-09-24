// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Diagnostics;
using Common;
using Microsoft.Extensions.Logging;
using Quartz;

public class ModuleScopeJobSchedulingBehavior(
    ILoggerFactory loggerFactory,
    IEnumerable<ActivitySource> activitySources = null) : JobSchedulingBehaviorBase(loggerFactory)
{
    private readonly IEnumerable<ActivitySource> activitySources = activitySources;

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

            var jobId = context.JobDetail.JobDataMap?.GetString(Constants.JobIdKey) ?? context.FireInstanceId;
            var jobTypeName = context.JobDetail.JobType.FullName;
            var correlationId = context.Get(Constants.CorrelationIdKey) as string;
            var flowId = context.Get(Constants.FlowIdKey) as string;

            await this.activitySources.Find(module?.Name)
                .StartActvity($"MODULE {module?.Name}",
                    async (a, c) =>
                    {
                        using (this.Logger.BeginScope(new Dictionary<string, object>
                               {
                                   [Constants.TraceIdKey] = a.TraceId.ToString()
                               }))
                        {
                            await this.activitySources.Find(module?.Name)
                                .StartActvity($"JOB_EXECUTE {jobTypeName}",
                                    async (a, c) => await next().AnyContext(),
                                    tags: new Dictionary<string, string>
                                        {
                                            ["job.id"] = jobId, ["job.type"] = jobTypeName
                                        },
                                    cancellationToken: c);
                        }
                    },
                    baggages: new Dictionary<string, string>
                    {
                        [ActivityConstants.ModuleNameTagKey] = module?.Name,
                        [ActivityConstants.CorrelationIdTagKey] = correlationId,
                        [ActivityConstants.FlowIdTagKey] = flowId
                    });
        }
    }
}