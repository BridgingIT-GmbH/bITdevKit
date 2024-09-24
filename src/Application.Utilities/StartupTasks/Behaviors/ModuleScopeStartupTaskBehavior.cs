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
    IEnumerable<IModuleContextAccessor> moduleAccessors = null) : StartupTaskBehaviorBase(loggerFactory)
{
    private readonly IEnumerable<ActivitySource> activitySources = activitySources;
    private readonly IEnumerable<IModuleContextAccessor> moduleAccessors = moduleAccessors;

    public override async Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next)
    {
        var module = this.moduleAccessors.Find(task.GetType());

        using (this.Logger.BeginScope(new Dictionary<string, object>
               {
                   [ModuleConstants.ModuleNameKey] = module?.Name ?? ModuleConstants.UnknownModuleName
               }))
        {
            if (module?.Enabled == false)
            {
                throw new ModuleNotEnabledException(module.Name);
            }

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
                                .StartActvity($"STARTUPTASK_EXECUTE {task.GetType().Name}",
                                    async (a, c) => await next().AnyContext(),
                                    cancellationToken: c);
                        }
                    },
                    baggages: new Dictionary<string, string> { [ActivityConstants.ModuleNameTagKey] = module?.Name });
        }
    }
}