// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class ModuleScopeBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    IEnumerable<IModuleContextAccessor> moduleAccessors = null,
    IEnumerable<ActivitySource> activitySources = null) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly IEnumerable<IModuleContextAccessor> moduleAccessors = moduleAccessors;
    private readonly IEnumerable<ActivitySource> activitySources = activitySources;
    private const string ModuleNameLogKey = "ModuleName";

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return true; // Always process, no attribute required
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        var module = this.moduleAccessors?.Find(request.GetType());
        var moduleName = module?.Name ?? "UnknownModule";

        using (this.Logger.BeginScope(new Dictionary<string, object>
        {
            [ModuleNameLogKey] = moduleName
        }))
        {
            if (module?.Enabled == false)
            {
                throw new ModuleNotEnabledException(moduleName);
            }

            return await next();
        }
    }
}
