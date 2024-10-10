// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Diagnostics;

public class ModuleScopeJobSchedulingBehavior(
    ILoggerFactory loggerFactory,
    IEnumerable<ActivitySource> activitySources = null)
    : JobSchedulingBehaviorBase(loggerFactory)
{
    public override async Task Execute(IJobExecutionContext context, JobDelegate next)
    {
        var moduleAccessors = context.Get("ModuleContextAccessors") as IEnumerable<IModuleContextAccessor>;
        var module = moduleAccessors.Find(context.JobDetail.JobType);
        var moduleName = module?.Name ?? ModuleConstants.UnknownModuleName;

        using (this.Logger.BeginScope(new Dictionary<string, object>
               {
                   [ModuleConstants.ModuleNameKey] = moduleName,
                   // correlatioid/flowid added earlier in the job execution pipeline (jobwrapper)
               }))
        {
            if (module is not null && !module.Enabled)
            {
                throw new ModuleNotEnabledException(moduleName);
            }

            var jobId = context.JobDetail.JobDataMap?.GetString(Constants.JobIdKey) ?? context.FireInstanceId;
            var jobTypeName = context.JobDetail.JobType.FullName;
            var correlationId = context.Get(Constants.CorrelationIdKey) as string;
            var flowId = context.Get(Constants.FlowIdKey) as string;

            await activitySources.Find(moduleName)
                .StartActvity($"MODULE {moduleName}",
                    async (a, c) =>
                    {
                        using (this.Logger.BeginScope(new Dictionary<string, object>
                               {
                                   [Constants.TraceIdKey] = a.TraceId.ToString()
                               }))
                        {
                            await Activity.Current.StartActvity($"JOB_EXECUTE {jobTypeName} [{moduleName}]",
                                    async (a, c) => await next().AnyContext(),
                                    ActivityKind.Producer,
                                    tags: new Dictionary<string, string>
                                    {
                                        ["job.id"] = jobId, ["job.type"] = jobTypeName
                                    },
                                    cancellationToken: c).AnyContext();
                        }
                    },
                    baggages: new Dictionary<string, string>
                    {
                        [ActivityConstants.ModuleNameTagKey] = moduleName,
                        [ActivityConstants.CorrelationIdTagKey] = correlationId,
                        [ActivityConstants.FlowIdTagKey] = flowId
                    });
        }
    }
}