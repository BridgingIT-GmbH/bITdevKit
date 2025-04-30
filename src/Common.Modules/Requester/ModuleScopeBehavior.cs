// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Requester;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Requester;
using Microsoft.Extensions.Logging;

public class ModuleScopeBehavior<TRequest, TResponse> : PipelineBehaviorBase<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    private readonly IEnumerable<IModuleContextAccessor> moduleAccessors;
    private readonly IEnumerable<ActivitySource> activitySources;

    public ModuleScopeBehavior(
        ILoggerFactory loggerFactory,
        IEnumerable<IModuleContextAccessor> moduleAccessors = null,
        IEnumerable<ActivitySource> activitySources = null)
        : base(loggerFactory)
    {
        this.moduleAccessors = moduleAccessors;
        this.activitySources = activitySources;
    }

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
            ["ModuleName"] = moduleName
        }))
        {
            if (module != null && !module.Enabled)
            {
                throw new ModuleNotEnabledException(moduleName);
            }

            return await next();
        }
    }
}
