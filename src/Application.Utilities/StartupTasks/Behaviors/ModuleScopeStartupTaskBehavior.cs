// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;

using System.Diagnostics;
using Common;
using Microsoft.Extensions.Logging;

public class ModuleScopeStartupTaskBehavior(
    ILoggerFactory loggerFactory,
    IEnumerable<ActivitySource> activitySources = null,
    IEnumerable<IModuleContextAccessor> moduleAccessors = null)
    : StartupTaskBehaviorBase(loggerFactory)
{
    public override async Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next)
    {
        var correlationId = GuidGenerator.CreateSequential().ToString("N");
        var flowId = GuidGenerator.Create(task.GetType().ToString()).ToString("N");
        var taskName = task.GetType().PrettyName();
        var module = moduleAccessors.Find(task.GetType());
        var moduleName = module?.Name ?? ModuleConstants.UnknownModuleName;

        using (this.Logger.BeginScope(new Dictionary<string, object>
               {
                   [ModuleConstants.ModuleNameKey] = moduleName,
                   [Constants.CorrelationIdKey] = correlationId,
                   [Constants.FlowIdKey] = flowId,
                   [Constants.StartupTaskKey] = taskName
               }))
        {
            if (module?.Enabled == false)
            {
                throw new ModuleNotEnabledException(moduleName);
            }

            await activitySources.Find(moduleName)
                .StartActvity($"MODULE {moduleName}",
                    async (a, c) =>
                    {
                        using (this.Logger.BeginScope(new Dictionary<string, object>
                               {
                                   [Constants.TraceIdKey] = a.TraceId.ToString()
                               }))
                        {
                            await Activity.Current
                                .StartActvity($"JOB_EXECUTE {taskName}",
                                    async (a, c) => await next().AnyContext(),
                                    tags: new Dictionary<string, string>
                                    {
                                        ["startuptask.type"] = taskName
                                    },
                                    cancellationToken: c).AnyContext();
                        }
                    },
                    baggages: new Dictionary<string, string>
                    {
                        [ActivityConstants.ModuleNameTagKey] = moduleName,
                        [ActivityConstants.CorrelationIdTagKey] = correlationId,
                        [ActivityConstants.FlowIdTagKey] = flowId
                    },
                    cancellationToken: cancellationToken).AnyContext();
        }
    }
}