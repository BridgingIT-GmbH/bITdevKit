// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using MediatR;
using Microsoft.Extensions.Logging;

public class ModuleScopeCommandBehavior<TRequest, TResponse> : CommandBehaviorBase<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    private readonly IEnumerable<IModuleContextAccessor> moduleAccessors;
    private readonly IEnumerable<ActivitySource> activitySources;

    public ModuleScopeCommandBehavior(
        ILoggerFactory loggerFactory,
        IEnumerable<IModuleContextAccessor> moduleAccessors = null,
        IEnumerable<ActivitySource> activitySources = null)
        : base(loggerFactory)
    {
        this.moduleAccessors = moduleAccessors;
        this.activitySources = activitySources;
    }

    protected override bool CanProcess(TRequest request)
    {
        return true;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var module = this.moduleAccessors.Find(request.GetType());

        using (this.Logger.BeginScope(new Dictionary<string, object>
        {
            [ModuleConstants.ModuleNameKey] = module?.Name ?? ModuleConstants.UnknownModuleName,
        }))
        {
            if (module is not null && !module.Enabled)
            {
                throw new ModuleNotEnabledException(module.Name);
            }
            else
            {
                return await next().AnyContext();
            }
        }
    }
}